﻿
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class SAV_FloatScript : UdonSharpBehaviour
{
    [SerializeField] private Rigidbody VehicleRigidbody;
    [SerializeField] private UdonSharpBehaviour SAVControl;
    [Tooltip("Transforms at which floating forces are calculate, recomd using 4 in a rectangle centered around the center of mass")]
    [SerializeField] private Transform[] FloatPoints;
    [Tooltip("Layers to raycast against to check for 'water'")]
    [SerializeField] private LayerMask FloatLayers = 16;
    private Transform VehicleTransform;
    [Tooltip("Multiplier for the forces pushing up")]
    [SerializeField] private float FloatForce = 5;
    [Tooltip("Max possible value to increase force by based on depth. Prevent objects from moving way too fast if dragged to the bottom of the water")]
    [SerializeField] private float MaxDepthForce = 25;
    [Tooltip("Value that the floating forces are multiplied by while vehicle is moving down in water. Higher = more stable floating")]
    [SerializeField] private float Compressing = 25;
    [Tooltip("Prevent extra force from compression becoming too high if the object is teleported deep underwater")]
    [SerializeField] private float MaxCompressingForce = 25;
    [Tooltip("Physical size of the simulated spherical float in meters")]
    [SerializeField] private float FloatRadius = .6f;
    [Tooltip("Strength of drag force applied by perpendicular movement in water (applied at floats)")]
    [SerializeField] private float WaterSidewaysDrag = 1f;
    [Tooltip("Strength of drag force applied by forward movement in water (applied at floats)")]
    [SerializeField] private float WaterForwardDrag = .05f;
    [Tooltip("Strength of force slowing rotation in water")]
    [SerializeField] private float WaterRotDrag = 5;
    [Tooltip("Strength of drag force slowing down the vehicle (applied at center of mass, causes no rotation)")]
    [SerializeField] private float WaterVelDrag = 0f;
    [SerializeField] private float WaveHeight = .6f;
    [Tooltip("Size of wave noise pattern. Smaller Number = bigger pattern")]
    [SerializeField] private float WaveScale = .04f;
    [Tooltip("How fast waves scroll across the sea")]
    [SerializeField] private float WaveSpeed = 12;
    [Tooltip("'Float' on solid objects (non-trigger) (used by hoverbikes)")]
    [SerializeField] private bool DoOnLand = false;
    [Tooltip("If a player takes ownership of the vehicle while its floats are below the water, the new owner will not know they are below the water and it will fall through the water. Move the vehicle up by this amount to prevent this from happening.")]
    public float MoveUpOnTakeOwnerShip = 2f;
    [Header("HoverBike Only")]
    [Tooltip("If hoverbike, script is only active when being piloted, also adds steering effects when near the ground")]
    public bool HoverBike = false;
    [Tooltip("Disable ground detection on attached vehicle (disable 'taxiing' movement)")]
    [SerializeField] private bool DisableGroundDetection = false;
    [Tooltip("If hoverbike, there are some 'unrealistic' turning physics when near the ground. This multiplies the strength of the rolling-into-a-turn extra turning ability")]
    [SerializeField] private float HoverBikeTurningStrength = 20;
    [Tooltip("If hoverbike, there are some 'unrealistic' turning physics when near the ground. This multiplies the strength of the drifintg-at-90-degrees extra turning ability")]
    [SerializeField] private float BackThrustStrength = 15;
    private float[] FloatDepth;
    private float[] FloatDepthLastFrame;
    private float[] FloatLastRayHitHeight;
    private float[] FloatTouchWaterPoint;
    private bool[] HitLandLast;
    private Vector3[] FloatLocalPos;
    private Vector3[] FloatPointForce;
    private int currentfloatpoint;
    [System.NonSerializedAttribute] public float depth;
    private VRCPlayerApi localPlayer;
    private bool InEditor = false;
    private float FloatDiameter;
    private int FPLength;
    void Start()
    {
        FPLength = FloatPoints.Length;
        FloatDiameter = FloatRadius * 2;

        localPlayer = Networking.LocalPlayer;
        if (localPlayer == null)
        { InEditor = true; }

        VehicleTransform = VehicleRigidbody.transform;

        int numfloats = FloatPoints.Length;
        FloatDepth = new float[numfloats];
        FloatDepthLastFrame = new float[numfloats];
        FloatLastRayHitHeight = new float[numfloats];
        FloatTouchWaterPoint = new float[numfloats];
        FloatPointForce = new Vector3[numfloats];
        FloatLocalPos = new Vector3[numfloats];
        HitLandLast = new bool[numfloats];
        for (int i = 0; i != numfloats; i++)
        {
            FloatLocalPos[i] = FloatPoints[i].localPosition;
        }
        if (HoverBike || (!InEditor && !localPlayer.isMaster))
        {
            gameObject.SetActive(false);
        }
        if (HoverBike || DisableGroundDetection)
        {
            SAVControl.SetProgramVariable("DisableGroundDetection", (int)SAVControl.GetProgramVariable("DisableGroundDetection") + 1);
            SAVControl.SetProgramVariable("Taxiing", false);
        }
    }
    public void SFEXT_O_TakeOwnership()
    {
        if (!HoverBike) { gameObject.SetActive(true); }

        Vector3 vehpos = VehicleTransform.position;
        vehpos.y += MoveUpOnTakeOwnerShip;
        VehicleTransform.position = vehpos;
    }
    public void SFEXT_O_LoseOwnership()
    {
        if (!HoverBike) { gameObject.SetActive(false); }
    }
    public void SFEXT_O_PilotExit()
    {
        if (HoverBike) { gameObject.SetActive(false); }
    }
    public void SFEXT_O_PilotEnter()
    {
        if (HoverBike) { gameObject.SetActive(true); }
    }
    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        if (!HoverBike && localPlayer.IsOwner(VehicleRigidbody.gameObject))
        { gameObject.SetActive(true); }
    }
    private void FixedUpdate()
    {
        Vector3 Vel = VehicleRigidbody.velocity;
        Vector3 TopOfFloat = FloatPoints[currentfloatpoint].position + (Vector3.up * FloatRadius);
        Vector3 Waves = Vector3.zero;
        if (!HitLandLast[currentfloatpoint])
        {
            float time = Time.time;
            //waves = (noise(+-0.5) * waveheight)
            Waves = (Vector3.up * ((Mathf.PerlinNoise(((TopOfFloat.x + (time * WaveSpeed)) * WaveScale), ((TopOfFloat.z + (time * WaveSpeed)) * WaveScale)) * WaveHeight) - .5f));
        }
        ///if above water, trace down to find water
        //if touching/in water trace down from diameterx2 above last water height at current xz to find water
        RaycastHit hit;
        FloatTouchWaterPoint[currentfloatpoint] = FloatLastRayHitHeight[currentfloatpoint] + FloatDiameter + Waves.y;
        if (FloatTouchWaterPoint[currentfloatpoint] > TopOfFloat.y)
        {
            //Touching or under water
            if (DoOnLand || !HitLandLast[currentfloatpoint])
            {
                FloatDepth[currentfloatpoint] = FloatTouchWaterPoint[currentfloatpoint] - TopOfFloat.y;
                float CompressionDifference = ((FloatDepth[currentfloatpoint] - FloatDepthLastFrame[currentfloatpoint]));
                if (CompressionDifference > 0)
                { CompressionDifference = Mathf.Min(CompressionDifference * Compressing, MaxCompressingForce); }
                else
                {
                    CompressionDifference = 0;
                }
                FloatDepthLastFrame[currentfloatpoint] = FloatDepth[currentfloatpoint];
                FloatPointForce[currentfloatpoint] = Vector3.up * (((Mathf.Min(FloatDepth[currentfloatpoint], MaxDepthForce) * FloatForce) + CompressionDifference));
                //float is potentially below the top of the trigger, so fire a raycast from above the last known trigger height to check if it's still there
                //the '+10': larger number means less chance of error if moving faster on a sloped water trigger, but could cause issues with bridges etc
                Vector3 checksurface = new Vector3(TopOfFloat.x, FloatLastRayHitHeight[currentfloatpoint] + 10, TopOfFloat.z);
                if (Physics.Raycast(checksurface, -Vector3.up, out hit, 14, FloatLayers, QueryTriggerInteraction.Collide))
                {
                    if (DoOnLand || hit.collider.isTrigger)
                    { FloatLastRayHitHeight[currentfloatpoint] = hit.point.y; ; }
                }
                else
                {
                    FloatLastRayHitHeight[currentfloatpoint] = float.MinValue;
                }
            }
        }
        else
        {
            //In Air
            FloatDepth[currentfloatpoint] = 0;
            FloatDepthLastFrame[currentfloatpoint] = 0;
            FloatPointForce[currentfloatpoint] = Vector3.zero;
            if (Vel.y > 0 || HitLandLast[currentfloatpoint])//only reset water level if moving up (or last hit was land), so things don't break if we go straight from air all the way to under the water
            { FloatTouchWaterPoint[currentfloatpoint] = float.MinValue; }
            //Debug.Log(string.Concat(currentfloatpoint.ToString(), ": Air: floatpointforce: ", FloatPointForce[currentfloatpoint].ToString()));
            //float could be below the top of the trigger if the waves are big enough, check for water trigger with current position + waveheight
            Vector3 checksurface = new Vector3(TopOfFloat.x, TopOfFloat.y + WaveHeight, TopOfFloat.z);
            if (Physics.Raycast(TopOfFloat, -Vector3.up, out hit, 35, FloatLayers, QueryTriggerInteraction.Collide))
            {
                FloatTouchWaterPoint[currentfloatpoint] = hit.point.y + FloatDiameter;
                HitLandLast[currentfloatpoint] = !hit.collider.isTrigger;
                FloatLastRayHitHeight[currentfloatpoint] = hit.point.y;
            }
            else
            {
                FloatLastRayHitHeight[currentfloatpoint] = float.MinValue;
            }
        }

        depth = 0;
        for (int i = 0; i != FloatDepth.Length; i++)
        {
            depth += FloatDepth[i];
        }
        float DepthMaxd = Mathf.Min(depth, MaxDepthForce);
        if (depth > 0)
        {//apply last calculated floating force for each floatpoint to respective floatpoints
            for (int i = 0; i != FloatPoints.Length; i++)
            {
                VehicleRigidbody.AddForceAtPosition(FloatPointForce[i], FloatPoints[i].position, ForceMode.Acceleration);
            }
            VehicleRigidbody.AddTorque(-VehicleRigidbody.angularVelocity * DepthMaxd * WaterRotDrag);
            VehicleRigidbody.AddForce(-VehicleRigidbody.velocity * DepthMaxd * WaterVelDrag);
            if (SAVControl && !HoverBike) { SAVControl.SetProgramVariable("Floating", true); }


            Vector3 right = VehicleTransform.right;
            Vector3 forward = VehicleTransform.forward;

            float sidespeed = Vector3.Dot(Vel, right);
            float forwardspeed = Vector3.Dot(Vel, forward);

            if (HoverBike)
            {
                Vector3 up = VehicleTransform.up;
                float RightY = Mathf.Abs(right.y);
                right.y = 0;
                if (Vector3.Dot(Vel, -up) > 0)
                {
                    right = right.normalized * (1 + (RightY * HoverBikeTurningStrength));
                }
                else
                {
                    right = Vector3.zero;
                }
                float BackThrustAmount = -((Vector3.Dot(Vel, forward)) * BackThrustStrength);
                if (BackThrustAmount > 0)
                { VehicleRigidbody.AddForce(forward * BackThrustAmount * DepthMaxd * (float)SAVControl.GetProgramVariable("ThrottleInput"), ForceMode.Acceleration); }
                VehicleRigidbody.AddForce(right * -sidespeed * WaterSidewaysDrag * DepthMaxd, ForceMode.Acceleration);
            }
            else
            {
                VehicleRigidbody.AddForceAtPosition(right * -sidespeed * WaterSidewaysDrag * DepthMaxd, FloatPoints[currentfloatpoint].position, ForceMode.Acceleration);
            }
            VehicleRigidbody.AddForceAtPosition(forward * -forwardspeed * WaterForwardDrag * DepthMaxd, FloatPoints[currentfloatpoint].position, ForceMode.Acceleration);

        }
        else
        { if (SAVControl && !HoverBike) { SAVControl.SetProgramVariable("Floating", false); } }

        currentfloatpoint++;
        if (currentfloatpoint == FPLength) { currentfloatpoint = 0; }
    }
}
