This current repository has a CDK style of deployment of a .NET 10 API to AWS.
We are using CDK for now to bring up the entire infrastructure quickly and be able ot tear it down and do this several times to test and build things in a predictable way.

This repo's core feature is to demonstrate Observability with OTel + AWS ADOT Collector + AMP (APS) + AMG (Dashboard)

I am building a new .NET 10 API in another repo which already has Observabaility baked in. This repo is to help me test and build Infra needed for setting up things for other repo.

The current repo I use to do a `CDK Deploy` to bring everything up and then I do some manual configuration.
Few manual things I do are as follows:
1. Install IIS (via PowerShell)
2. Add a new WebSite for this test project
3. Install .NET 10 Hosting Runtime bundle (via WinGet) (CLI: `winget install Microsoft.DotNet.HostingBundle.10 --source winget`)
4. Install AWS ADOT Collector and configure it's config.yaml (add/update AMP/APS write endpoint + see: ./config/adot-collector-aws-production.yaml). Install from: https://aws-otel-collector.s3.amazonaws.com/windows/amd64/v0.47.0/aws-otel-collector.msi
5. Copy over the .NET API binaries to the new site.
----
6. Add myself as the user for AMG Workspace
7. Configure Datasource for Prometheus
8. Add a new Dashboard (from ./config/grafana/dashboards/amg-dashboard.json)
9. Run ./scripts/generate-traffic.sh to generate some traffic and see results in dashboard (this is purely testing purpose)

Based on all of the above, I am planning to build a runbook for production situation, where I would like to do the following:
1. Create a new WebSite in IIS and deploy the new API
2. Install .NET 10 Hosting bundle
3. Configure/Setup stuff necessary to let traffic from ALB to this new site via Host header.
4. Setup the necessary access for .NET API and AWS ADOT Collector to write or access stuff outside the VPC for Otel export
5. Setup AMP/APS workspace + access to it via IAM, etc.
6. Install AWS ADOT Collector and configure it with Dev/QA env AMP/APS write endpoint
7. Setup new AMG workspace
8. Configure AMG for Prometheus datasource
9. Configure AMG for new Dashboard (most probably that will be the same fiel from here)