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
        dir('backend') {
          // restore and build from backend root
          sh 'dotnet restore'
          sh 'dotnet build --configuration Release'
          // run tests from test project directory
          sh 'dotnet test Tests/DeploymentRisk.Api.Tests/DeploymentRisk.Api.Tests.csproj --no-build --verbosity normal'
        }
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
    // this says: always look for test results and save build artifacts (can be found in Jenkins UI)
    always {
      junit '**/TestResults/*.xml'
      archiveArtifacts artifacts: '**/coverage/**, **/TestResults/**', allowEmptyArchive: true
    }
  }
}
