* Generate report of commit details that are not present in your previous release branch but in master(develop version) branch without the upcomming release tagged work item or without work item itself. 
* This can help you to know the unintended change set of your upcoming release.
* Provide values to the Config file variables and do `dotnet run` to get your unintended release commits report generated.
* Approximately 190 new commits can be recognized by this project, to increase this limit change the MaxDiffNumber in the Config file.
