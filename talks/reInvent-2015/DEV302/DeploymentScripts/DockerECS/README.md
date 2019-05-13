# Pollster ECS Docker environment

Use the PowerShell script EnvironmentSetup.ps1 to setup a ECS environment that Pollster can be deployed to. This script runs a CloudFormation template that launches 3 EC2 instances and 4 load balancer, one for each web process of Pollster. The PowerShell script will then go on to create the ECS Cluster, task definitions and services with names matching Visual Studio projects.

Be sure to tear down the environment when you are done testing because charges will happen as the AWS resources continue to run.  Use the EnvironmentTearDown.ps1 to teminate the CloudFormation stack and clean up the ECS resources.