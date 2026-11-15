namespace PageTrail.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<PageTrailApiFactory>
{
    public const string Name = "Api";
}
