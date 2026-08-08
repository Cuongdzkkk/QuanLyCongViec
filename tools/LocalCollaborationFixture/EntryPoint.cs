namespace LocalCollaborationFixture;

public static class EntryPoint
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = FixtureOptions.Parse(args);
            var identity = FixtureIdentity.For(options.Prefix);
            await using var factory = new LocalApiFactory(options);
            using var startupClient = factory.CreateClient();
            var store = new FixtureStore(factory.Services, identity);

            Console.WriteLine($"Target verified: configured local SQL Server / TaskManagementDB / Integrated Security / {options.EnvironmentName}");
            Console.WriteLine($"Fixture prefix: {options.Prefix}");

            switch (options.Command)
            {
                case "provision":
                    await store.ProvisionAsync();
                    await store.AssertMatrixAsync();
                    Console.WriteLine("PASS fixture provisioned idempotently; no credentials or tokens were emitted");
                    break;

                case "cleanup":
                    await store.CleanupAsync();
                    await store.AssertCleanAsync();
                    Console.WriteLine("PASS fixture cleanup removed only run-scoped roots and dependencies");
                    break;

                case "smoke":
                    try
                    {
                        await store.CleanupAsync();
                        await store.AssertCleanAsync();
                        await store.ProvisionAsync();
                        await store.AssertMatrixAsync();
                        await new RuntimeSmoke(factory, identity).RunAsync();
                        Console.WriteLine("PASS local multi-user collaboration runtime smoke");
                    }
                    finally
                    {
                        if (!options.Keep)
                        {
                            await store.CleanupAsync();
                            await store.AssertCleanAsync();
                            Console.WriteLine("PASS automatic run-scoped cleanup");
                        }
                        else
                        {
                            Console.WriteLine("Fixture retained by explicit --keep; run cleanup with the same run ID.");
                        }
                    }
                    break;
            }

            return 0;
        }
        catch (FixtureUsageException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
        catch (FixtureSafetyException exception)
        {
            Console.Error.WriteLine($"SAFETY_REFUSAL: {exception.Message}");
            return 3;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL: {exception.GetType().Name}: {exception.Message}");
            return 1;
        }
    }
}
