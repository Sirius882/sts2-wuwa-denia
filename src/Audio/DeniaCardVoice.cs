using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace Denia;

/// <summary>
/// 卡牌打出时的语音/音效播放。
/// 对应关系（卡牌类名 -> 音频文件名）在构建期通过卡牌标题与 sounds/ 下音频做模糊匹配后写死，
/// 运行时仅做查表播放，绝不参与任何游戏逻辑结算。
///
/// 播放语义：
///   - 打出的卡有配音 -> 先 Stop 打断上一个仍在播放的音频，再播放新的（单例播放器保证互斥）；
///   - 打出的卡无配音 -> 直接返回，不打断、不播放（保持上一个音频继续播完）。
/// </summary>
public static class DeniaCardVoice
{
    // 卡牌类名 -> 音频文件名（不含扩展名，对应 res://sounds/{key}.wav）
    // 由构建期模糊匹配确定（含 6 张靠标点/问号/波浪号区分的卡牌）。
    private static readonly Dictionary<string, string> CardVoiceMap = new()
    {
        { "DeniaBehave",              "乖" },
        { "DeniaYouTryIt",            "你也试试" },
        { "DeniaBorrowMe",            "借我用下" },
        { "DeniaRadianceDissolved",   "光辉自此消融" },
        { "DeniaItsMyTurn",           "到我的回合啦" },
        { "DeniaSwallow",             "吞没" },
        { "DeniaObey",                "听话" },
        { "DeniaFinishQuickly",       "快点结束吧" },
        { "DeniaPityMe",              "怜悯我" },
        { "DeniaExile",               "放逐" },
        { "DeniaImmerseIntoVoid",     "没入虚无" },
        { "DeniaDeepEternal",         "深黯终末恒常" },
        { "DeniaSmelt",               "熔毁" },
        { "DeniaMeltingAway",         "熔毁殆尽" },
        { "DeniaLookAtMe",            "直视我" },
        { "DeniaRupture",             "破裂" },
        { "DeniaKeepRunning",         "继续逃啊" },
        { "DeniaPleaseDoNot",         "请您不要" },
        { "DeniaPleaseDoNotForgiveMe","请您不要···宽恕我" },
        { "DeniaCrush",               "轧碎" },
        { "DeniaGiveItBack",          "还给你" },
        { "DeniaSendYouIn",           "送你进去" },
    };

    private const string SoundFolder = "res://sounds/";
    private static AudioStreamPlayer? _player;
    private static readonly Dictionary<string, AudioStream> StreamCache = new();

    /// <summary>
    /// 在卡牌打出后调用。同步、即燃即弃，内部已吞掉所有异常，绝不影响游戏逻辑。
    /// </summary>
    public static void TryPlay(CardModel card)
    {
        if (card == null) return;
        if (!CardVoiceMap.TryGetValue(card.GetType().Name, out var file))
            return; // 该卡无配音 -> 不打断、不播放
        try
        {
            Play(file);
        }
        catch
        {
            // 音频失败绝不向上抛，避免影响游戏
        }
    }

    private static void Play(string file)
    {
        var path = SoundFolder + file + ".wav";
        if (!StreamCache.TryGetValue(path, out var stream))
        {
            stream = GD.Load<AudioStream>(path);
            if (stream != null)
                StreamCache[path] = stream;
        }
        if (stream == null) return;

        EnsurePlayer();
        if (_player == null) return;
        _player.Stop();        // 打断上一个仍在播放的音频
        _player.Stream = stream;
        _player.Play();
    }

    private static void EnsurePlayer()
    {
        if (_player != null && GodotObject.IsInstanceValid(_player))
            return;
        var tree = Engine.GetMainLoop() as SceneTree;
        var root = tree?.Root;
        if (root == null) return;
        _player = new AudioStreamPlayer
        {
            Name = "DeniaCardVoice",
            Bus = "SFX",
        };
        root.AddChild(_player);
    }
}
