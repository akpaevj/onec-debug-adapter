namespace Onec.DebugAdapter.V8
{
    public record InfoBaseItem(string Name, IReadOnlyDictionary<string, string?> Properties)
    {
        public string? Connect => Properties.TryGetValue("Connect", out var value) switch
        {
            true => value,
            _ => null
        };
    }
}
