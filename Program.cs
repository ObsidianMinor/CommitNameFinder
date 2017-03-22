using System;
using Octokit;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.CommandLineUtils;

namespace CommitNameFinder
{
    class Program
    {
        static void Main(string[] args)
        {
            string accessToken = null, userId = null;
            bool forks = false;
            CommandLineApplication application = new CommandLineApplication(false);
            CommandArgument userIdArg = application.Argument("user", "The username of the user you want to search.", false);
            CommandOption accessTokenOp = application.Option("-t |--token <access_token>", "A token for a github account. This will run requests authenticated as the account the token comes from, increasing the hourly rate limit to 50,000 requests", CommandOptionType.SingleValue);
            CommandOption searchForksOp = application.Option("-f |--forks", "Search all repositories including forks.", CommandOptionType.NoValue);
            application.HelpOption("-? | -h | --help");
            application.OnExecute(() =>
            {
                if(string.IsNullOrWhiteSpace(userIdArg.Value))
                {
                    application.ShowHelp();
                    return 1;
                }
                userId = userIdArg.Value;
                if (searchForksOp.HasValue())
                    forks = true;
                if (accessTokenOp.HasValue())
                    accessToken = accessTokenOp.Value();
                return 0;
            });

            if(application.Execute(args) != 0)
                return;
            HashSet<string> foundNames = new HashSet<string>();

            try
            {
                new Program().StartAsync(foundNames, accessToken, userId, forks).GetAwaiter().GetResult();
            }
            catch(NotFoundException)
            {
                Console.WriteLine($"Could not find repositories for {userId}");
            }
            catch(RateLimitExceededException)
            {
                Console.WriteLine("The rate limit was exceded!");
                WriteNames(foundNames);
            }
            catch(Exception e)
            {
                Console.WriteLine($"An exception occured while searching: {e.Message}");
                Console.Write(e.StackTrace);
            }
        }

        async Task StartAsync(HashSet<string> foundNames, string accessToken, string userId, bool searchForks)
        {
            Console.WriteLine("Starting github client...");
            GitHubClient github = (accessToken == null) ? new GitHubClient(new ProductHeaderValue("CommitNameFinder")) : new GitHubClient(new ProductHeaderValue("CommitNameFinder")) { Credentials = new Credentials(accessToken) };
            var repos = (await github.Repository.GetAllForUser(userId)).Where(repo => searchForks || !repo.Fork);

            Console.WriteLine($"Found {repos.Count()} repositories");
            
            foreach(var repo in repos)
            {
                Console.WriteLine($"Getting commits for {repo.FullName}");
                var repoCommits = await github.Repository.Commit.GetAll(repo.Id);
                Console.WriteLine($"Processing commits...");

                foreach(GitHubCommit commit in repoCommits.Where(repoCommit => string.Equals(repoCommit.Author?.Login, userId, StringComparison.InvariantCultureIgnoreCase)))
                {
                    Console.WriteLine($"Processing commit {commit.Sha}...");
                    foundNames.Add(commit.Commit.Committer.Name);
                }
            }

            WriteNames(foundNames);
            
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        static void WriteNames(HashSet<string> names)
        {
            Console.WriteLine($"Name search complete! Found {names.Count} unique names");
            foreach (var name in names)
                Console.WriteLine($"\t{name}");
        }
    }
}
