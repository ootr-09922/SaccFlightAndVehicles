
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class SaccTarget : UdonSharpBehaviour
{
    public float HitPoints = 100f;
    public UdonSharpBehaviour[] ExplodeOther;
    private Animator TargetAnimator;
    private float FullHealth;
    private VRCPlayerApi localPlayer;
    void Start()
    {
        TargetAnimator = gameObject.GetComponent<Animator>();
        FullHealth = HitPoints;
        localPlayer = Networking.LocalPlayer;
    }
    void OnParticleCollision(GameObject other)//hit by bullet
    {
        if (other == null) return;

        if (HitPoints <= 10f)//hit does 10 damage, so we're dead
        {
            if (localPlayer == null)//editor
            {
                Explode();
            }
            else//ingame
            {
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "Explode");
            }
        }
        else
        {
            if (localPlayer == null)//editor
            {
                TargetTakeDamage();
            }
            else//ingame
            {
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "TargetTakeDamage");
            }
        }
    }
    public void TargetTakeDamage()
    {
        HitPoints -= 10;
    }
    public void Explode()
    {
        TargetAnimator.SetTrigger("explode");
        HitPoints = FullHealth;
        foreach (UdonSharpBehaviour Exploder in ExplodeOther)
        {
            if (Exploder != null)
            {
                Exploder.SendCustomEvent("Explode");
            }
        }
    }
}