/*
Declarative Jenkins pipeline for CI (build + test).

Notes:
- This pipeline assumes the Jenkins agent has dotnet and Node.js installed.
- The pipeline performs:
  1. Checkout
  2. Backend: restore, build, run tests
  3. Frontend: npm install, run tests
  4. (optional) Build images with docker-compose
*/

pipeline {
  // run on any available jenkins agent (REQUIRED)
  agent any
  // .NET needs this to work without ICU library
  environment {
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT = 'true'
  }
  // enable timestamps + color support
  options {
    timestamps()
    ansiColor('xterm')
  }
  stages {

    // checkout code
    stage('Checkout') {
      steps {
        checkout scm
      }
    }

    // Backend
    stage('Backend: Restore, Build, Test') {
      steps {
        // restore and build from repo root (where .sln file is)
        sh 'dotnet restore deployment-risk-platform.sln'
        sh 'dotnet build deployment-risk-platform.sln --configuration Release'
        // run backend tests from test project (builds test project if needed)
        sh 'dotnet test backend/Tests/DeploymentRisk.Api.Tests/DeploymentRisk.Api.Tests.csproj --verbosity normal'
      }
    }

    // frontend
    stage('Frontend: Install & Test') {
      steps {
        dir('frontend/deployment-risk-ui') {
          // Use Node.js/npm on the agent
          sh 'npm ci'
          // Run tests 
          sh 'npm test -- --watch=false'
        }
      }
    }

    // This stage requires Docker on the Jenkins agent and appropriate permissions
    stage('Optional: Build Docker Compose') {
      when {
        expression { return env.BUILD_IMAGES == 'true' }
      }
      steps {
        sh 'docker compose -f ${WORKSPACE}/docker-compose.yml build --pull'
      }
    }
  }

  // happens after pipeline
  post {
    // collect test results and artifacts if available
    always {
      junit testResults: '**/TestResults/*.xml', allowEmptyResults: true
      archiveArtifacts artifacts: '**/coverage/**, **/TestResults/**', allowEmptyArchive: true
    }
  }
}
