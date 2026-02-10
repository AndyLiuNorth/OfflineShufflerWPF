namespace ShufflerWPF.IntegrationTests;
using Xunit;

public class TrueBoxFixtrue
{
    public TrueBoxFixtrue()
    {
        TrueBoxTestHost.EnsureStartedAsync();
    }
}

[CollectionDefinition("TrueBox")]
public class TrueBoxCollection : ICollectionFixture<TrueBoxFixtrue>{}

