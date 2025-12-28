import os
import pandas as pd
from github import Github
import logging

# Configure logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)

def fetch_data(repo_name, limit=500):
    token = os.getenv("GITHUB_TOKEN")
    if not token:
        logger.error("GITHUB_TOKEN environment variable not set.")
        return

    logger.info(f"Connecting to GitHub to fetch history for {repo_name}...")
    g = Github(token)
    repo = g.get_repo(repo_name)
    
    data = []
    
    # optimize: get closed PRs
    pulls = repo.get_pulls(state='closed', sort='created', direction='desc')
    
    count = 0
    logger.info(f"Scanning last {limit} closed PRs...")
    
    for pr in pulls:
        if count >= limit:
            break
            
        if not pr.merged:
            continue
            
        # Feature Extraction
        # We need to approximate 'Test Pass Rate' if CI data isn't easily available.
        # For now, we'll assume 1.0 (pass) for merged PRs, unless we find failure comments.
        
        created_at = pr.created_at
        
        # Risk Labeling Heuristic:
        # 1. Was this PR reverted? (Check if a subsequent PR says "Revert #123")
        # 2. This is hard to check perfectly efficiently, so we simplify:
        #    If the PR title starts with "Revert", the *target* it reverted was risky.
        #    But for this dataset, we want to know if *this* PR is risky.
        #    A better proxy: 
        #    - If it's a "Hotfix", it implies the previous state was risky, but the hotfix itself might be risky too (rushed).
        #    - Real "Reverted" status is best checked via events, but slow.
        
        # Simpler Heuristic for Demo:
        # We assume a PR is "High Risk" if it is very large (lines > 1000) or pushed on weekends,
        # AND (crucially) if it has "fix" or "bug" in the title of the *next* PR by the same author (indicating a follow-up fix).
        # OR: We just look for "Revert" in the timeline.
        
        is_risky = 0.0
        
        # Check for 'Revert' PRs that reference this one would be ideal, 
        # but let's assume if the PR title contains "hotfix" it's high risk (rushed).
        if "hotfix" in pr.title.lower():
            is_risky = 0.8
            
        # If the PR took a very long time to merge (> 7 days), maybe it was complex/risky?
        merge_time = (pr.merged_at - pr.created_at).total_seconds() / 3600 # hours
        
        row = {
            'commit_count': pr.commits,
            'lines_changed': pr.additions + pr.deletions,
            'test_pass_rate': 1.0, # Default for merged PRs
            'hour_of_day': created_at.hour,
            'day_of_week': created_at.weekday(),
            'merge_time_hours': merge_time,
            'risk_score': is_risky # Target variable
        }
        
        data.append(row)
        count += 1
        if count % 50 == 0:
            logger.info(f"Processed {count} PRs...")

    df = pd.DataFrame(data)
    
    # If we didn't find many "risky" ones via heuristics, we might need to manually adjust
    # or rely on the distribution (Anomaly Detection).
    # For now, let's normalize risk based on complexity if 'is_risky' is all 0.
    if df['risk_score'].sum() == 0:
        logger.info("No obvious 'Reverts' found. Using complexity metrics as risk proxy.")
        # Normalize lines_changed to 0-1
        df['risk_score'] = (df['lines_changed'] / df['lines_changed'].max()) * 0.5
        # Add time penalty
        df.loc[df['day_of_week'] >= 5, 'risk_score'] += 0.2
        
    output_file = "data/historical_pr_data.csv"
    os.makedirs("data", exist_ok=True)
    df.to_csv(output_file, index=False)
    logger.info(f"Saved {len(df)} records to {output_file}")

if __name__ == "__main__":
    # Example usage: set GITHUB_TOKEN and run
    # GITHUB_TOKEN=... python fetch_historical_data.py
    import sys
    if len(sys.argv) < 2:
        print("Usage: python fetch_historical_data.py <owner/repo>")
        sys.exit(1)
        
    fetch_data(sys.argv[1])
