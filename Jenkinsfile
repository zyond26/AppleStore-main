pipeline {
    agent any

    environment {
        DOTNET_CLI_HOME = "C:\\Program Files\\dotnet"
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        stage('Build') {
            steps {
                script {
                    // Restoring dependencies
                    //bat "cd ${DOTNET_CLI_HOME} && dotnet restore"
                    bat "dotnet restore"

                    // Building the application
                    bat "dotnet build --configuration Release"
                }
            }
        }

        stage('Test') {
            steps {
                script {
                    // Running tests
                    bat "dotnet test --no-restore --configuration Release"
                }
            }
        }

        stage('Publish') {
            steps {
                script {
                    // Publishing the application
                    bat "dotnet publish --no-restore --configuration Release --output .\\publish"
                }
            }
        }
        stage('Deploy') {
            steps {
                script {
                    withCredential([usernamePassword(credentialId: 'coreuser',passwordVariable: 'CREDENTIAL_PASSWORD',usernameVariable: 'CREDENTIAL_USERNAME')]){
                    powershell '''

                    $credentials = New-Object System.Management.Automation.PSCredential($env:CREDENTIAL_USERNAME,(ConverTo-SecureString $env:CREDENTIAL_PASSWORD -AsPlainText -Force))
                    
                    Copy-Item -path 'publish\\*' -Destination 'X:\' -force


                    }
                    }
                    }
                    }

    post {
        success {
            echo 'Build, test, and publish successful!'
        }
    }
}
                    }
                    
                  