pipeline {
  agent any
  stages {
    stage('Build') {
      steps {
        git(url: 'https://github.com/Chainium/Chainium.git', branch: 'master', poll: true)
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