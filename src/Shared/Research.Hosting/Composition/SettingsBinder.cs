using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Research.Hosting.Composition;

/// <summary>
/// Binds an options section twice: into a validated startup snapshot (for decisions made at composition time) and
/// as reloading <see cref="IOptionsMonitor{TOptions}"/> validated on start and on every reload. Each Host's
/// <c>Settings</c> class calls <see cref="Add{TOptions}"/> once per section.
/// </summary>
public sealed class SettingsBinder(IServiceCollection services, IConfiguration configuration)
{
    public TOptions Add<TOptions>(string section, params IValidateOptions<TOptions>[] validators)
        where TOptions : class, new()
    {
        ArgumentNullException.ThrowIfNull(validators);
        var configurationSection = configuration.GetSection(section);

        var snapshot = new TOptions();
        configurationSection.Bind(snapshot);
        var failures = validators
            .Select(validator => validator.Validate(Options.DefaultName, snapshot))
            .Where(static result => result.Failed)
            .SelectMany(static result => result.Failures ?? [])
            .ToList();

        if (failures.Count > 0)
        {
            throw new OptionsValidationException(Options.DefaultName, typeof(TOptions), failures);
        }

        services.AddOptions<TOptions>().Bind(configurationSection).ValidateOnStart();
        foreach (var validator in validators)
        {
            services.AddSingleton(validator);
        }

        return snapshot;
    }
}
