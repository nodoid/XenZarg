namespace XenZarg.Core.Audio;

public interface ISoundPlayer
{
    void Play(Sfx sfx, float pitch = 0f);
}

public sealed class SilentSoundPlayer : ISoundPlayer
{
    public static readonly SilentSoundPlayer Instance = new();
    public void Play(Sfx sfx, float pitch = 0f) { }
}
