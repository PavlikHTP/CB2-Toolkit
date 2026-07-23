using System.Reflection;
using CB2Toolkit.Core.Console.Abstraction;

namespace CB2Toolkit.Core.Services;

public class CommandRegistry
{
    private readonly Dictionary<string, IConsoleCommand> _commands = new(StringComparer.OrdinalIgnoreCase);

    public void Register<T>() where T : IConsoleCommand, new()
    {
        Register(new T());
    }

    public void Register(IConsoleCommand command)
    {
        _commands[command.Name] = command;
        foreach (var alias in command.Aliases)
        {
            _commands[alias] = command;
        }
    }

    public void RegisterFromAssembly(Assembly assembly)
    {
        var types = assembly.GetTypes()
            .Where(t => typeof(IConsoleCommand).IsAssignableFrom(t) 
                        && !t.IsInterface 
                        && !t.IsAbstract 
                        && t.GetConstructor(Type.EmptyTypes) != null);

        foreach (var type in types)
        {
            if (Activator.CreateInstance(type) is IConsoleCommand cmd)
            {
                Register(cmd);
            }
        }
    }

    public bool TryGetCommand(string name, out IConsoleCommand? command) => _commands.TryGetValue(name, out command);

    public IEnumerable<IConsoleCommand> GetAllCommands() => _commands.Values.Distinct();
}