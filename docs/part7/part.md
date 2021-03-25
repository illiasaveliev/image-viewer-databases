# Part 7 – Clean-up resources

With a help of **CloudFormation** we can clean all the created resources within a few minutes.

1. Open **AWS Console**
2. Go to the **CloudFormation** service
3. Delete all created stacks
    - **image-viewer-api**
    - **image-viewer-web-app**
    - **image-viewer-labeling-function**
    - **image-viewer-top-tags-function**
4. Go to the **Lambda** service and check that all resources were deleted, if no please delete them manually
5. Go to the **Cognito User Pools** and delete **image-cognito-pool**
6. Go to the **S3** and check that all test buckets were removed
7. Go to the **RDS** and remove Aurora Serverless cluster
8. Go to the **DynamoDB** and remove all created tables.
