pipeline {
  agent any
  stages {
    stage('Build') {
      steps {
        git(url: 'https://github.com/Chainium/Chainium.git', branch: 'master', credentialsId: '8475894346057f343aafe756b4857ba634b243ca', poll: true)
      }
    }
    stage('Unit tests') {
      steps {
        sh 'echo "TODO: run unit tests"'
      }
    }
    stage('Integration tests') {
      steps {
        sh 'echo "Run integration tests"'
      }
    }
  }
}