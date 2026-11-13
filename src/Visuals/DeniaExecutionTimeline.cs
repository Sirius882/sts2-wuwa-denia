namespace Denia;

/// <summary>独立于解码帧率的演出时序；原结算只等待到最后白闪。</summary>
internal static class DeniaExecutionTimeline
{
    public const float Enter = 24f / 30f;
    public const float Video = 137f / 30f;
    public const float Impact = 8f / 30f;
    public const float Release = Enter + Video + Impact;
    public const float Return = 0.5f;
    public const float Total = Release + Return;
    public const string VideoPath = "res://videos/denia_execution_middle.ogv";

    public static bool ShouldPlay(int bias, bool dead, bool immune) => bias >= 3 && !dead && !immune;
}
