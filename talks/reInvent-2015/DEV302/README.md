# Hosting ASP.NET 5 applications in AWS with Docker and AWS CodeDeploy

For this talk we developed the application Pollster which is an application for hosting polls. 

## Setting up AWS Resources

Pollster uses Amazon DynamoDB, Amazon Simple Workflow and Amazon Simple Notification Service. To setup the AWS resources used by Pollster execute the SetupApplicationAWSResources.ps1 script from the DeploymentScripts folder.

## Project Layout

Pollster is an example of a microservice design. There are 5 separate processes that are part of Pollster.

### PollFeed

This process is a MVC Web API project that returns the current list of active polls. In local development mode this is setup to run with kestrel on port 5002.

### PollVoter

This process is a MVC Web API project that processes the vote submissions. In local development mode this is setup to run with kestrel on port 5003.

### PollWebFrontend

This process is a MVC Web Application that is the main entrance for Pollster. In local development mode this is setup to run with kestrel on port 5000. In development the frontend makes web requests to the other services using there deveplopment port numbers.

### PollWorkflow

This process is a ASP.NET 5 console application. PollWorkflow is the scheduling engine that polls Simple Workflow to determine if polls should be moved to the active list or expired and send out the results.

### PollWriter

This process is a MVC Web API project that contains API for creating new Polls. In local development mode this is setup to run with kestrel on port 5001.

## DeploymentScripts

As part of the re:Invent session pollster was deployed to AWS CodeDeploy and an EC2 Container Service cluster. The DeploymentScripts folder contains the scripts to create those environments. To see how Pollster deployment setup works view the video our re:Invent session. TODO: insert url once video is posted