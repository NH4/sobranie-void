namespace Sobranie.Infrastructure;

public sealed class SobranieOptions
{
    public const string SectionName = "Sobranie";

    public string DatabasePath { get; set; } = "sobranie.db";

    public OllamaOptions Ollama { get; set; } = new();

    public FsmOptions Fsm { get; set; } = new();
}

public sealed class OllamaOptions
{
    public string Endpoint { get; set; } = "http://127.0.0.1:11434";
    public string Model { get; set; } = "yak8b:q4km";
    public int NumCtx { get; set; } = 4096;
    public double Temperature { get; set; } = 0.8;
    public double TopP { get; set; } = 0.9;
    public double RepeatPenalty { get; set; } = 1.1;
    public int MaxOutputTokens { get; set; } = 180;
}

public sealed class FsmOptions
{
    public double MinCadenceSeconds { get; set; } = 45;
    public double MaxCadenceSeconds { get; set; } = 60;

    public double SoftmaxTemperature { get; set; } = 0.6;

    public double RecencyPenaltyHalfLifeSpeeches { get; set; } = 3.0;
    public double RecencyPenaltyMagnitude { get; set; } = 2.0;

    public double TraitMatchWeight { get; set; } = 1.5;
    public double BaseUtility { get; set; } = 1.0;

    public int RecentSpeechWindow { get; set; } = 8;

    public int? RandomSeed { get; set; }

    public bool AutoStartSession { get; set; }
}
