# Deploying the GitHub App
Included in this repository is a kubernetes spec files that can be used to deploy the GitHub App to a Kubernetes cluster. It also includes two spec files for for mounting a persistent volume from the node to the pod. This is not necessary at the moment as the secrets and variables are pass directly to the container. 

## Requirements
- A Kubernetes cluster
- (kubectl)[https://kubernetes.io/docs/tasks/tools/install-kubectl/] installed and configured to connect to the cluster

## Configuration
The following variables are required to be set in the appropriate files before deploying the application to the cluster:

- appsettings.Production.json
    - GitHub:ApiUrl - The URL for the API of the GHES instance or GitHub.com (e.g. https://ghes.localdev.me/api/v3)
    - GitHub:ClientSecret - The secret provided by GitHub when registering the GitHub App (e.g. 1wWM1l943)
    - GitHub:ApplicationId - The Application ID of the GitHub App
    - GitHub:PrivateKey - The path to the private key provided by GitHub when registering the GitHub App
    - S3Bucket:AccessKeySecret - The AccessKeyId for a IAM user with access to the S3 bucket (AKIAIOSFODNN7EXAMPLE)
    - S3Bucket:SecretAccessKeySecret - The SecretAccessKey for a IAM user with access to the S3 bucket (e.g iubLrxqf9Q/0kmxyexample26Qu/6Ibi1mynyg4o)
    - S3Bucket:EndPoint - The endpoint url for the S3 bucket (e.g. https://s3.us-east-2.amazonaws.com)
    - Splunk:Endpoint - The endpoint url for the Splunk instance (e.g. https://splunk.prod.com:8088)
    - Splunk:Source - The sourcetype for the Splunk instance (e.g. github-archiver)
    - Splunk:Token - The token for the Splunk instance (e.g. 1234-5678-9012-3456)

All traffic from a proxy can be forwarded to port 80. A health check is avaiable on `${hostname}:80/api/status`. 

## Deployment
Once all of the config values are updated, you can deploy the application to the cluster by running the following command:

    kubectl apply -f prod-deploy.yml


