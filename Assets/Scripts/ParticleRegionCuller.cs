using UnityEngine;

/// <summary>
/// アタッチされた ParticleSystem の粒子のうち、
/// 指定領域 (X <= hideXMax かつ Z >= hideZMin) に入ったものを毎フレーム消滅させる。
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class ParticleRegionCuller : MonoBehaviour
{
    public float hideXMax;
    public float hideZMin;

    private ParticleSystem ps;
    private ParticleSystem.Particle[] buffer;

    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
    }

    void LateUpdate()
    {
        if (ps == null) return;

        int count = ps.particleCount;
        if (count == 0) return;

        if (buffer == null || buffer.Length < count)
            buffer = new ParticleSystem.Particle[count];

        int actual = ps.GetParticles(buffer);
        bool isLocalSpace = ps.main.simulationSpace == ParticleSystemSimulationSpace.Local;
        Transform tr = ps.transform;
        bool changed = false;

        for (int i = 0; i < actual; i++)
        {
            Vector3 worldPos = isLocalSpace ? tr.TransformPoint(buffer[i].position) : buffer[i].position;
            if ((worldPos.x <= hideXMax || worldPos.z >= hideZMin) && buffer[i].remainingLifetime > 0f)
            {
                ParticleSystem.Particle p = buffer[i];
                p.remainingLifetime = 0f;
                buffer[i] = p;
                changed = true;
            }
        }

        if (changed)
            ps.SetParticles(buffer, actual);
    }
}
