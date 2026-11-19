namespace Research.Hosting.Composition;

/// <summary>
/// One capability's service registrations. A feature registers services only: configuration is bound by the Host's
/// <c>Settings</c> class, and everything whose order matters (middleware, endpoints) stays in the Host's composition
/// root, where the whole order can be read in one place.
/// </summary>
public interface IResearchFeature
{
    void Register(ResearchBuilder app);
}
