namespace Sobranie.Infrastructure;

public sealed class SobranieOptions
{
    public const string SectionName = "Sobranie";

    public string DatabasePath { get; set; } = "sobranie.db";

    public OllamaOptions Ollama { get; set; } = new();
}

public sealed class OllamaOptions
{
    public string Endpoint { get; set; } = "http://127.0.0.1:11434";
    public string Model { get; set; } = "yak8b:q4km";
    public int NumCtx { get; set; } = 4096;
    public double Temperature { get; set; } = 0.8;
    public double TopP { get; set; } = 0.9;
    public double RepeatPenalty { get; set; } = 1.1;
}
