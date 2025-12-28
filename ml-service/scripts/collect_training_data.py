#!/usr/bin/env python3
"""
Historical PR Data Collection Script for ML Model Training

This script collects historical pull request data from GitHub repositories
and labels them with risk scores based on heuristic analysis.

Usage:
    python collect_training_data.py owner/repo --limit 1000

Requirements:
    - GITHUB_TOKEN environment variable (personal access token)
    - PyGithub: pip install PyGithub pandas

Risk Labeling Strategy:
    1. PRs that were later reverted = HIGH RISK (0.8-1.0)
    2. PRs with follow-up fixes within 24h = MEDIUM-HIGH RISK (0.5-0.7)
    3. "hotfix", "urgent", "critical" in title = MEDIUM RISK (0.4-0.6)
    4. Large PRs (>1000 lines) + weekend/after-hours = MEDIUM RISK (0.4-0.6)
    5. Small PRs with tests + business hours = LOW RISK (0.0-0.3)
"""

import argparse
import os
import sys
import pandas as pd
from github import Github, GithubException
import logging
import re
from datetime import datetime, timedelta

# Configure logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)


def analyze_revert_prs(pulls):
    """
    Build a map of PRs that were later reverted.
    Returns dict: {pr_number: True} for reverted PRs
    """
    revert_map = {}
    logger.info("Analyzing for revert PRs...")

    for pr in pulls:
        if pr.merged and "revert" in pr.title.lower():
            # Try to extract the PR number being reverted
            # Common patterns: "Revert #123" or "Revert PR #123"
            match = re.search(r'#(\d+)', pr.title)
            if match:
                reverted_pr_num = int(match.group(1))
                revert_map[reverted_pr_num] = True
                logger.debug(f"Found revert: PR #{pr.number} reverts PR #{reverted_pr_num}")

    logger.info(f"Found {len(revert_map)} reverted PRs")
    return revert_map


def analyze_followup_fixes(pulls_list):
    """
    Detect PRs that had follow-up fix PRs within 24 hours by the same author.
    Returns dict: {pr_number: True} for PRs with quick fixes
    """
    followup_map = {}
    logger.info("Analyzing for follow-up fix PRs...")

    # Sort by merge time
    merged_prs = [(pr, pr.merged_at) for pr in pulls_list if pr.merged and pr.merged_at]
    merged_prs.sort(key=lambda x: x[1])

    for i, (pr, merge_time) in enumerate(merged_prs):
        # Look at next few PRs by same author within 24h
        for j in range(i + 1, min(i + 10, len(merged_prs))):
            next_pr, next_merge_time = merged_prs[j]

            # Check if same author and within 24h
            if next_pr.user.login == pr.user.login:
                time_diff = next_merge_time - merge_time
                if time_diff <= timedelta(hours=24):
                    # Check if it's a fix (look for keywords)
                    fix_keywords = ['fix', 'bug', 'hotfix', 'patch', 'correct', 'repair']
                    if any(keyword in next_pr.title.lower() for keyword in fix_keywords):
                        followup_map[pr.number] = True
                        logger.debug(f"PR #{pr.number} had follow-up fix #{next_pr.number} within 24h")
                        break

    logger.info(f"Found {len(followup_map)} PRs with follow-up fixes")
    return followup_map


def calculate_risk_score(pr, revert_map, followup_map):
    """
    Calculate risk score (0.0 to 1.0) for a PR based on heuristics.
    """
    risk_score = 0.0

    # 1. Was this PR reverted? CRITICAL RISK
    if pr.number in revert_map:
        return 0.9

    # 2. Had follow-up fix within 24h? MEDIUM-HIGH RISK
    if pr.number in followup_map:
        return 0.6

    # 3. Hotfix/urgent keywords in title? MEDIUM RISK
    urgent_keywords = ['hotfix', 'urgent', 'critical', 'emergency', 'asap']
    if any(keyword in pr.title.lower() for keyword in urgent_keywords):
        risk_score += 0.3

    # 4. Calculate size impact
    lines_changed = pr.additions + pr.deletions
    if lines_changed > 2000:
        risk_score += 0.35
    elif lines_changed > 1000:
        risk_score += 0.25
    elif lines_changed > 500:
        risk_score += 0.15
    elif lines_changed > 200:
        risk_score += 0.08

    # 5. Many commits = potential churn
    if pr.commits > 30:
        risk_score += 0.20
    elif pr.commits > 20:
        risk_score += 0.15
    elif pr.commits > 10:
        risk_score += 0.10

    # 6. Weekend deployment (Saturday=5, Sunday=6)
    created_time = pr.created_at
    if created_time.weekday() in [5, 6]:
        risk_score += 0.20

    # 7. Friday deployment
    if created_time.weekday() == 4:
        risk_score += 0.10

    # 8. After-hours (before 8am or after 6pm)
    if created_time.hour < 8 or created_time.hour > 18:
        risk_score += 0.15

    # 9. Check for critical files (if we can get file list)
    try:
        files = list(pr.get_files())
        critical_keywords = ['migration', 'database', 'auth', '.sql', 'config', 'schema']
        critical_count = sum(1 for f in files if any(kw in f.filename.lower() for kw in critical_keywords))
        if critical_count > 0:
            risk_score += min(critical_count * 0.15, 0.30)
    except Exception:
        pass  # File list not available

    # 10. Low risk bonus: small PRs during business hours
    if (lines_changed < 100 and
        pr.commits <= 3 and
        9 <= created_time.hour <= 17 and
        created_time.weekday() < 5):
        risk_score -= 0.10

    # Clip to valid range
    risk_score = max(0.0, min(risk_score, 1.0))

    return risk_score


def collect_data(repo_name, limit=1000, output_file="data/historical_pr_data.csv"):
    """
    Collect historical PR data from GitHub repository.
    """
    token = os.getenv("GITHUB_TOKEN")
    if not token:
        logger.error("GITHUB_TOKEN environment variable is required")
        sys.exit(1)

    logger.info(f"Connecting to GitHub API...")
    try:
        g = Github(token)
        repo = g.get_repo(repo_name)
        logger.info(f"Repository: {repo.full_name}")
    except GithubException as e:
        logger.error(f"Failed to access repository: {e}")
        sys.exit(1)

    logger.info(f"Fetching up to {limit} closed PRs...")
    pulls = repo.get_pulls(state='closed', sort='created', direction='desc')

    # First pass: collect merged PRs
    logger.info("Collecting merged PRs...")
    merged_prs = []
    count = 0

    for pr in pulls:
        if count >= limit:
            break

        if pr.merged:
            merged_prs.append(pr)
            count += 1

            if count % 100 == 0:
                logger.info(f"Collected {count}/{limit} PRs...")

    logger.info(f"Collected {len(merged_prs)} merged PRs")

    # Second pass: analyze for reverts and follow-up fixes
    revert_map = analyze_revert_prs(merged_prs)
    followup_map = analyze_followup_fixes(merged_prs)

    # Third pass: extract features and calculate risk scores
    logger.info("Extracting features and calculating risk scores...")
    data = []

    for pr in merged_prs:
        try:
            risk_score = calculate_risk_score(pr, revert_map, followup_map)

            data.append({
                'pr_number': pr.number,
                'title': pr.title,
                'author': pr.user.login if pr.user else 'unknown',
                'commit_count': pr.commits,
                'lines_changed': pr.additions + pr.deletions,
                'lines_added': pr.additions,
                'lines_deleted': pr.deletions,
                'test_pass_rate': 1.0,  # Assume passed if merged
                'hour_of_day': pr.created_at.hour,
                'day_of_week': pr.created_at.weekday(),
                'created_at': pr.created_at.isoformat(),
                'merged_at': pr.merged_at.isoformat() if pr.merged_at else None,
                'risk_score': round(risk_score, 3),
                'was_reverted': pr.number in revert_map,
                'had_followup_fix': pr.number in followup_map
            })

        except Exception as e:
            logger.warning(f"Error processing PR #{pr.number}: {e}")
            continue

    # Convert to DataFrame
    df = pd.DataFrame(data)

    # Create output directory
    os.makedirs(os.path.dirname(output_file), exist_ok=True)

    # Save to CSV
    df.to_csv(output_file, index=False)
    logger.info(f"\nSaved {len(df)} PRs to {output_file}")

    # Print statistics
    print("\n" + "="*60)
    print("COLLECTION SUMMARY")
    print("="*60)
    print(f"Repository: {repo_name}")
    print(f"Total PRs collected: {len(df)}")
    print(f"\nRisk Distribution:")
    print(f"  LOW (<0.3):      {len(df[df['risk_score'] < 0.3]):4d} ({len(df[df['risk_score'] < 0.3])/len(df)*100:5.1f}%)")
    print(f"  MEDIUM (0.3-0.5):{len(df[(df['risk_score'] >= 0.3) & (df['risk_score'] < 0.5)]):4d} ({len(df[(df['risk_score'] >= 0.3) & (df['risk_score'] < 0.5)])/len(df)*100:5.1f}%)")
    print(f"  HIGH (0.5-0.8):  {len(df[(df['risk_score'] >= 0.5) & (df['risk_score'] < 0.8)]):4d} ({len(df[(df['risk_score'] >= 0.5) & (df['risk_score'] < 0.8)])/len(df)*100:5.1f}%)")
    print(f"  CRITICAL (>0.8): {len(df[df['risk_score'] >= 0.8]):4d} ({len(df[df['risk_score'] >= 0.8])/len(df)*100:5.1f}%)")
    print(f"\nSpecial Cases:")
    print(f"  Reverted PRs:       {len(df[df['was_reverted']])}")
    print(f"  Follow-up fixes:    {len(df[df['had_followup_fix']])}")
    print(f"\nStats:")
    print(f"  Avg commits/PR:     {df['commit_count'].mean():.1f}")
    print(f"  Avg lines changed:  {df['lines_changed'].mean():.0f}")
    print(f"  Avg risk score:     {df['risk_score'].mean():.3f}")
    print("="*60)
    print(f"\nNext steps:")
    print(f"  1. Review the data: cat {output_file}")
    print(f"  2. Train model: python train_xgboost_model.py")
    print(f"  3. Restart ML service: docker-compose restart ml-service")
    print("="*60)


def main():
    parser = argparse.ArgumentParser(
        description="Collect historical PR data from GitHub for ML model training",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Collect 1000 PRs from Microsoft's VS Code repo
  python collect_training_data.py microsoft/vscode --limit 1000

  # Collect 500 PRs and save to custom file
  python collect_training_data.py facebook/react --limit 500 --output data/react_prs.csv

Environment:
  GITHUB_TOKEN    Personal access token (required)
                  Create at: https://github.com/settings/tokens
        """
    )

    parser.add_argument("repo", help="Repository in format 'owner/repo'")
    parser.add_argument("--limit", type=int, default=1000,
                        help="Number of PRs to fetch (default: 1000)")
    parser.add_argument("--output", default="data/historical_pr_data.csv",
                        help="Output CSV file path (default: data/historical_pr_data.csv)")

    args = parser.parse_args()

    # Validate repo format
    if '/' not in args.repo:
        logger.error("Repository must be in format 'owner/repo'")
        sys.exit(1)

    collect_data(args.repo, args.limit, args.output)


if __name__ == "__main__":
    main()
