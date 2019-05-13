# Docker environment for AWS Elastic Beanstalk

There is no setup script for creating the AWS Elastic Beanstalk multi container environment. Selecting the Docker Multi Container from the AWS Elastic Beanstalk web console is the easiest way to get started. 

Be sure the IAM role used for the environment has access to the required resources Pollster needs. The file pollster-app-policy.json in the parent directory is a sample policy that gives permission to the AWS services Pollster needs.

The Dockerrun.aws.json file in this directory can be used to bootstrap your environment with Pollster with images pushed to Docker Hub