## Work with Net Core CLI 
You can use Net Core CLI to work with AWS Lambda and other services
https://docs.aws.amazon.com/toolkit-for-visual-studio/latest/user-guide/lambda-cli-publish.html

At first please check that **dotnet lambda** command is working, if not install it from here
https://github.com/aws/aws-extensions-for-dotnet-cli

## Deploy Functions

1. Create bucket for source code **image-viewer-code**. Choose your bucket name and region

   ```bash
   aws s3api create-bucket --bucket image-viewer-code --region eu-west-1 --create-bucket-configuration LocationConstraint=eu-west-1
   ```
2. Open command line in the project folder and execute the next command

   ```bash
   dotnet lambda deploy-serverless
   ```
3. Enter S3 bucket name that you created and follow other steps during deployment.
4. It is possible to deploy just function without CloudFormation stack using the next command
   ```bash
   dotnet lambda deploy-function
   ```