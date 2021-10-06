
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class SaccSeaVehicle : UdonSharpBehaviour
{
    [Tooltip("Base object reference")]
    public SaccEntity EntityControl;
    [Tooltip("The object containing all non-trigger colliders for the vehicle, their layers are changed when entering and exiting")]
    public Transform VehicleMesh;
    [Tooltip("Layer to set the colliders to when entering vehicle")]
    public int OnboardVehicleLayer = 19;
    [Tooltip("Position Thrust force is applied at")]
    public Transform ThrustPoint;
    [Tooltip("Position yawing forces are applied at")]
    public Transform YawMoment;
    [UdonSynced(UdonSyncMode.None)] public float Health = 23f;
    [Tooltip("Teleport the vehicle to the oposite side of the map when flying too far in one direction?")]
    public bool RepeatingWorld = true;
    [Tooltip("Distance you can travel away from world origin before being teleported to the other side of the map. Not recommended to increase, floating point innacuracy and game freezing issues may occur if larger than default")]
    public float RepeatingWorldDistance = 20000;
    [Tooltip("Use the left hand to control the joystick and the right hand to control the throttle?")]
    public bool SwitchHandsJoyThrottle = false;
    public bool HasAfterburner = true;
    public KeyCode AfterBurnerKey = KeyCode.T;
    [Tooltip("Point in the throttle at which afterburner enables, .8 = 80%")]
    public float ThrottleAfterburnerPoint = 0.8f;
    [Header("Response:")]
    [Tooltip("Vehicle thrust at max throttle without afterburner")]
    public float ThrottleStrength = 20f;
    [Tooltip("Multiply how much the VR throttle moves relative to hand movement")]
    public float ThrottleSensitivity = 6f;
    [Tooltip("How much more thrust the vehicle has when in full afterburner")]
    public float AfterburnerThrustMulti = 1.5f;
    [Tooltip("How quickly the vehicle throttles up after throttle is increased (Lerp)")]
    public float AccelerationResponse = 4.5f;
    [Tooltip("How quickly the vehicle throttles down relative to how fast it throttles up after throttle is decreased")]
    public float EngineSpoolDownSpeedMulti = .5f;
    [Tooltip("How much the plane slows down (Speed lerped towards 0)")]
    public float AirFriction = 0.0004f;
    [Tooltip("Yaw force multiplier, (gets stronger with airspeed)")]
    public float YawStrength = 3f;
    [Tooltip("Yaw rotation force (as multiple of YawStrength) (doesn't get stronger with airspeed, useful for helicopters and ridiculous jets). Setting this to a non - zero value disables inversion of joystick pitch controls when vehicle is travelling backwards")]
    public float YawThrustVecMulti = 0f;
    [Tooltip("Force that stops vehicle from yawing, (gets stronger with airspeed)")]
    public float YawFriction = 15f;
    [Tooltip("Force that stops vehicle from yawing, (doesn't get stronger with airspeed)")]
    public float YawConstantFriction = 0f;
    [Tooltip("How quickly the vehicle responds to changes in joystick's yaw (Lerp)")]
    public float YawResponse = 20f;
    [Tooltip("Adjust the rotation of Unity's inbuilt Inertia Tensor Rotation, which is a function of rigidbodies. If set to 0, the plane will be very stable and feel boring to fly.")]
    public float InertiaTensorRotationMulti = 1;
    [Tooltip("Rotational inputs are multiplied by current speed to make flying at low speeds feel heavier. Above the speed input here, all inputs will be at 100%. Linear. (Meters/second)")]
    public float RotMultiMaxSpeed = 10;
    [Tooltip("How much the the vehicle's nose is pulled toward the direction of movement on the yaw axis")]
    public float VelStraightenStrYaw = 0.045f;
    [Tooltip("Degrees per second the vehicle rotates on the ground. Uses simple object rotation with a lerp, no real physics to it.")]
    public float TaxiRotationSpeed = 35f;
    [Tooltip("How lerped the taxi movement rotation is")]
    public float TaxiRotationResponse = 2.5f;
    [Tooltip("Make taxiing more realistic by not allowing vehicle to rotate on the spot")]
    public bool DisallowTaxiRotationWhileStill = false;
    [Tooltip("When the above is ticked, This is the speed at which the plane will reach its full turning speed. Meters/second.")]
    public float TaxiFullTurningSpeed = 20f;
    [Tooltip("Push the vehicle up based on speed. Sit higher on the water when moving faster")]
    public float VelLift = 1f;
    [Tooltip("Maximum Vel Lift, to stop the nose being pushed up. Technically should probably be 9.81 to counter gravity exactly")]
    public float VelLiftMax = 10f;
    [Tooltip("Vehicle will take damage if experiences more Gs that this (Internally Gs are calculated in all directions, the HUD shows only vertical Gs so it will differ slightly")]
    public float MaxGs = 10f;
    [Tooltip("Damage taken Per G above maxGs, per second.\n(Gs - MaxGs) * GDamage = damage/second")]
    public float GDamage = 10f;
    [Header("Other:")]
    [Tooltip("Adjusts all values that would need to be adjusted if you changed the mass automatically on Start(). Including all wheel colliders suspension values")]
    [SerializeField] private bool AutoAdjustValuesToMass = true;
    [Tooltip("Transform to base the pilot's throttle and joystick controls from. Used to make vertical throttle for helicopters, or if the cockpit of your vehicle can move, on transforming vehicle")]
    public Transform ControlsRoot;
    [Tooltip("Wind speed on each axis")]
    public Vector3 Wind;
    [Tooltip("Strength of noise-based changes in wind strength")]
    public float WindGustStrength = 15;
    [Tooltip("How often wind gust changes strength")]
    public float WindGustiness = 0.03f;
    [Tooltip("Scale of world space gust cells, smaller number = larger cells")]
    public float WindTurbulanceScale = 0.0001f;
    [UdonSynced(UdonSyncMode.None)] public float Fuel = 900;
    [Tooltip("Amount of fuel at which throttle will start reducing")]
    public float LowFuel = 125;
    [Tooltip("Fuel consumed per second at max throttle, scales with throttle")]
    public float FuelConsumption = 2;
    [Tooltip("Multiply FuelConsumption by this number when at full afterburner Scales with afterburner level")]
    public float FuelConsumptionABMulti = 3f;
    [Tooltip("Number of resupply ticks it takes to refuel fully from zero")]
    public float RefuelTime = 25;
    [Tooltip("Number of resupply ticks it takes to repair fully from zero")]
    public float RepairTime = 30;
    [Tooltip("Time until vehicle reappears after exploding")]
    public float RespawnDelay = 10;
    [Tooltip("Time after reappearing the plane is invincible for")]
    public float InvincibleAfterSpawn = 2.5f;
    [Tooltip("Damage taken when hit by a bullet")]
    public float BulletDamageTaken = 10f;
    [Tooltip("Locally destroy target if prediction thinks you killed them, should only ever cause problems if you have a system that repairs vehicles during a fight")]
    public bool PredictDamage = true;
    [System.NonSerializedAttribute] public float AllGs;


    [System.NonSerializedAttribute] [UdonSynced(UdonSyncMode.Linear)] public float EngineOutput = 0f;
    [System.NonSerializedAttribute] public Vector3 CurrentVel = Vector3.zero;
    [System.NonSerializedAttribute] [UdonSynced(UdonSyncMode.Linear)] public float VertGs = 1f;
    [System.NonSerializedAttribute] [UdonSynced(UdonSyncMode.Linear)] public float AngleOfAttack;//MAX of yaw & pitch aoa //used by effectscontroller and hudcontroller
    [System.NonSerializedAttribute] public bool Occupied = false; //this is true if someone is sitting in pilot seat

    [System.NonSerializedAttribute] public Animator VehicleAnimator;
    [System.NonSerializedAttribute] public Rigidbody VehicleRigidbody;
    [System.NonSerializedAttribute] public Transform VehicleTransform;
    private VRC.SDK3.Components.VRCObjectSync VehicleObjectSync;
    private GameObject VehicleGameObj;
    [System.NonSerializedAttribute] public Transform CenterOfMass;
    private float LerpedYaw;
    [System.NonSerializedAttribute] public bool ThrottleGripLastFrame = false;
    [System.NonSerializedAttribute] public bool JoystickGripLastFrame = false;
    Quaternion JoystickZeroPoint;
    Quaternion PlaneRotLastFrame;
    [System.NonSerializedAttribute] public float PlayerThrottle;
    private float TempThrottle;
    private float ThrottleZeroPoint;
    [System.NonSerializedAttribute] public float ThrottleInput = 0f;
    private float roll = 0f;
    private float pitch = 0f;
    private float yaw = 0f;
    [System.NonSerializedAttribute] public float FullHealth;
    [System.NonSerializedAttribute] public bool Taxiing = false;
    [System.NonSerializedAttribute] public bool Floating = false;
    [System.NonSerializedAttribute] [UdonSynced(UdonSyncMode.Linear)] public Vector3 RotationInputs;
    [System.NonSerializedAttribute] public bool Piloting = false;
    [System.NonSerializedAttribute] public bool Passenger = false;
    [System.NonSerializedAttribute] public bool InEditor = true;
    [System.NonSerializedAttribute] public bool InVR = false;
    [System.NonSerializedAttribute] public Vector3 LastFrameVel = Vector3.zero;
    [System.NonSerializedAttribute] public VRCPlayerApi localPlayer;
    [System.NonSerializedAttribute] public float rotlift;
    private Vector3 Yawing;
    private Vector3 Thrust;
    [System.NonSerializedAttribute] public float Taxiinglerper;
    [System.NonSerializedAttribute] public float ExtraDrag = 1;
    [System.NonSerializedAttribute] public float ExtraLift = 1;
    [System.NonSerializedAttribute] public float Speed;
    [System.NonSerializedAttribute] public float AirSpeed;
    [System.NonSerializedAttribute] public bool IsOwner = false;
    private Vector3 FinalWind;//includes Gusts
    [System.NonSerializedAttribute] public Vector3 AirVel;
    private float StillWindMulti;//multiplies the speed of the wind by the speed of the plane when taxiing to prevent still planes flying away
    private float SoundBarrier;
    [System.NonSerializedAttribute] public float FullFuel;
    private float LowFuelDivider;
    private float LastResupplyTime = 5;//can't resupply for the first 10 seconds after joining, fixes potential null ref if sending something to PlaneAnimator on first frame
    [System.NonSerializedAttribute] public float FullGunAmmo;
    [System.NonSerializedAttribute] public Vector3 Spawnposition;
    [System.NonSerializedAttribute] public Quaternion Spawnrotation;
    [System.NonSerializedAttribute] public int OutsidePlaneLayer;
    [System.NonSerializedAttribute] public bool DoAAMTargeting;
    [System.NonSerializedAttribute] public Rigidbody GDHitRigidbody;
    [System.NonSerializedAttribute] public bool UsingManualSync;
    bool FloatingLastFrame = false;
    bool GroundedLastFrame = false;
    private float VelLiftStart;
    private int VehicleLayer;
    private float VelLiftMaxStart;
    private bool HasAirBrake;//set to false if air brake strength is 0
    private float HandDistanceZLastFrame;
    private float EngineAngle;
    private float PitchThrustVecMultiStart;
    private float YawThrustVecMultiStart;
    private float RollThrustVecMultiStart;
    private float ThrottleNormalizer;
    private float ABNormalizer;
    private float EngineOutputLastFrame;
    bool HasWheelColliders = false;
    private float TaxiFullTurningSpeedDivider;
    private bool LowFuelLastFrame;
    private bool NoFuelLastFrame;
    [System.NonSerializedAttribute] public float ThrottleStrengthAB;
    [System.NonSerializedAttribute] public float FuelConsumptionAB;
    [System.NonSerializedAttribute] public bool AfterburnerOn;
    [System.NonSerializedAttribute] public bool PitchDown;//air is hitting plane from the top
    private float GDamageToTake;
    [System.NonSerializedAttribute] public float LastHitTime = -100;
    [System.NonSerializedAttribute] public float PredictedHealth;


    [System.NonSerializedAttribute] public int NumActiveFlares;
    [System.NonSerializedAttribute] public int NumActiveChaff;
    [System.NonSerializedAttribute] public int NumActiveOtherCM;
    //this stuff can be used by DFUNCs
    //if these == 0 then they are not disabled. Being an int allows more than one extension to disable it at a time
    [System.NonSerializedAttribute] public float Limits = 1;
    [System.NonSerializedAttribute] public int DisablePhysicsAndInputs = 0;
    [System.NonSerializedAttribute] public int OverrideConstantForce = 0;
    [System.NonSerializedAttribute] public Vector3 CFRelativeForceOverride;
    [System.NonSerializedAttribute] public Vector3 CFRelativeTorqueOverride;
    [System.NonSerializedAttribute] public int DisableTaxiRotation = 0;
    [System.NonSerializedAttribute] public int DisableGroundDetection = 0;
    [System.NonSerializedAttribute] public int ThrottleOverridden = 0;
    [System.NonSerializedAttribute] public float ThrottleOverride;
    [System.NonSerializedAttribute] public int JoystickOverridden = 0;
    [System.NonSerializedAttribute] public Vector3 JoystickOverride;


    [System.NonSerializedAttribute] public int ReSupplied = 0;
    public void SFEXT_L_EntityStart()
    {
        VehicleGameObj = EntityControl.gameObject;
        VehicleTransform = EntityControl.transform;
        VehicleRigidbody = EntityControl.GetComponent<Rigidbody>();

        Spawnposition = VehicleTransform.position;
        Spawnrotation = VehicleTransform.rotation;

        localPlayer = Networking.LocalPlayer;
        if (localPlayer == null)
        {
            InEditor = true;
            Piloting = true;
            IsOwner = true;
            Occupied = true;
            VehicleRigidbody.drag = 0;
            VehicleRigidbody.angularDrag = 0;
        }
        else
        {
            InEditor = false;
            InVR = localPlayer.IsUserInVR();
            if (localPlayer.isMaster)
            {
                IsOwner = true;
                VehicleRigidbody.drag = 0;
                VehicleRigidbody.angularDrag = 0;
            }
            else
            {
                VehicleRigidbody.drag = 9999;
                VehicleRigidbody.angularDrag = 9999;
            }
        }

        WheelCollider[] wc = VehicleMesh.GetComponentsInChildren<WheelCollider>(true);
        if (wc.Length != 0) { HasWheelColliders = true; }

        if (AutoAdjustValuesToMass)
        {
            //values that should feel the same no matter the weight of the aircraft
            float RBMass = VehicleRigidbody.mass;
            ThrottleStrength *= RBMass;
            YawStrength *= RBMass;
            YawFriction *= RBMass;
            YawConstantFriction *= RBMass;
            VelStraightenStrYaw *= RBMass;
            VelLiftMax *= RBMass;
            foreach (WheelCollider wheel in wc)
            {
                JointSpring SusiSpring = wheel.suspensionSpring;
                SusiSpring.spring *= RBMass;
                SusiSpring.damper *= RBMass;
                wheel.suspensionSpring = SusiSpring;
            }
        }
        VehicleLayer = VehicleMesh.gameObject.layer;//get the layer of the plane as set by the world creator
        OutsidePlaneLayer = VehicleMesh.gameObject.layer;
        VehicleAnimator = EntityControl.GetComponent<Animator>();

        FullHealth = Health;
        FullFuel = Fuel;

        VelLiftMaxStart = VelLiftMax;
        VelLiftStart = VelLift;

        CenterOfMass = EntityControl.CenterOfMass;
        VehicleRigidbody.centerOfMass = VehicleTransform.InverseTransformDirection(CenterOfMass.position - VehicleTransform.position);//correct position if scaled
        VehicleRigidbody.inertiaTensorRotation = Quaternion.SlerpUnclamped(Quaternion.identity, VehicleRigidbody.inertiaTensorRotation, InertiaTensorRotationMulti);


        if (!HasAfterburner) { ThrottleAfterburnerPoint = 1; }
        ThrottleNormalizer = 1 / ThrottleAfterburnerPoint;
        ABNormalizer = 1 / (1 - ThrottleAfterburnerPoint);

        FuelConsumptionAB = (FuelConsumption * FuelConsumptionABMulti) - FuelConsumption;
        ThrottleStrengthAB = (ThrottleStrength * AfterburnerThrustMulti) - ThrottleStrength;

        VehicleObjectSync = (VRC.SDK3.Components.VRCObjectSync)EntityControl.gameObject.GetComponent(typeof(VRC.SDK3.Components.VRCObjectSync));
        if (VehicleObjectSync == null)
        {
            UsingManualSync = true;
        }

        LowFuelDivider = 1 / LowFuel;

        if (DisallowTaxiRotationWhileStill)
        {
            TaxiFullTurningSpeedDivider = 1 / TaxiFullTurningSpeed;
        }
        if (!ControlsRoot)
        { ControlsRoot = VehicleTransform; }
    }
    private void LateUpdate()
    {
        float DeltaTime = Time.deltaTime;
        if (IsOwner)//works in editor or ingame
        {
            if (!EntityControl.dead)
            {
                //G/crash Damage
                Health -= Mathf.Max((GDamageToTake) * DeltaTime * GDamage, 0f);//take damage of GDamage per second per G above MaxGs
                GDamageToTake = 0;
                if (Health <= 0f)//plane is ded
                {
                    SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(Explode));

                }
            }
            else { GDamageToTake = 0; }

            if (Floating)
            {
                if (!FloatingLastFrame)
                {
                    SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(TouchDownWater));
                }
            }
            else
            { FloatingLastFrame = false; }
            if (Taxiing && !GroundedLastFrame && !FloatingLastFrame)
            {
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(TakeOff));
            }

            //synced variables because rigidbody values aren't accessable by non-owner players
            CurrentVel = VehicleRigidbody.velocity;
            Speed = CurrentVel.magnitude;
            bool VehicleMoving = false;
            if (Speed > .1f)//don't bother doing all this for planes that arent moving and it therefore wont even effect
            {
                VehicleMoving = true;//check this bool later for more optimizations
                WindAndAoA();
            }

            if (Piloting)
            {
                //gotta do these this if we're piloting but it didn't get done(specifically, hovering extremely slowly in a VTOL craft will cause control issues we don't)
                if (!VehicleMoving)
                { WindAndAoA(); VehicleMoving = true; }
                if (RepeatingWorld)
                {
                    if (CenterOfMass.position.z > RepeatingWorldDistance)
                    {
                        Vector3 vehpos = VehicleTransform.position;
                        vehpos.z -= RepeatingWorldDistance * 2;
                        VehicleTransform.position = vehpos;
                    }
                    else if (CenterOfMass.position.z < -RepeatingWorldDistance)
                    {
                        Vector3 vehpos = VehicleTransform.position;
                        vehpos.z += RepeatingWorldDistance * 2;
                        VehicleTransform.position = vehpos;
                    }
                    else if (CenterOfMass.position.x > RepeatingWorldDistance)
                    {
                        Vector3 vehpos = VehicleTransform.position;
                        vehpos.x -= RepeatingWorldDistance * 2;
                        VehicleTransform.position = vehpos;
                    }
                    else if (CenterOfMass.position.x < -RepeatingWorldDistance)
                    {
                        Vector3 vehpos = VehicleTransform.position;
                        vehpos.x += RepeatingWorldDistance * 2;
                        VehicleTransform.position = vehpos;
                    }
                }

                if (DisablePhysicsAndInputs == 0)
                {
                    //collect inputs
                    int Wi = Input.GetKey(KeyCode.W) ? 1 : 0; //inputs as ints
                    int Si = Input.GetKey(KeyCode.S) ? -1 : 0;
                    int Ai = Input.GetKey(KeyCode.A) ? -1 : 0;
                    int Di = Input.GetKey(KeyCode.D) ? 1 : 0;
                    int Qi = Input.GetKey(KeyCode.Q) ? -1 : 0;
                    int Ei = Input.GetKey(KeyCode.E) ? 1 : 0;
                    int upi = Input.GetKey(KeyCode.UpArrow) ? 1 : 0;
                    int downi = Input.GetKey(KeyCode.DownArrow) ? -1 : 0;
                    int lefti = Input.GetKey(KeyCode.LeftArrow) ? -1 : 0;
                    int righti = Input.GetKey(KeyCode.RightArrow) ? 1 : 0;
                    bool Shift = Input.GetKey(KeyCode.LeftShift);
                    bool Ctrl = Input.GetKey(KeyCode.LeftControl);
                    int Shifti = Shift ? 1 : 0;
                    int LeftControli = Ctrl ? 1 : 0;
                    float LGrip = 0;
                    float RGrip = 0;
                    if (!InEditor)
                    {
                        LGrip = Input.GetAxisRaw("Oculus_CrossPlatform_PrimaryHandTrigger");
                        RGrip = Input.GetAxisRaw("Oculus_CrossPlatform_SecondaryHandTrigger");
                    }
                    //MouseX = Input.GetAxisRaw("Mouse X");
                    //MouseY = Input.GetAxisRaw("Mouse Y");
                    Vector3 JoystickPosYaw;
                    Vector3 JoystickPos;
                    Vector2 VRPitchRoll;

                    float ThrottleGrip;
                    float JoyStickGrip;
                    if (SwitchHandsJoyThrottle)
                    {
                        JoyStickGrip = LGrip;
                        ThrottleGrip = RGrip;
                    }
                    else
                    {
                        ThrottleGrip = LGrip;
                        JoyStickGrip = RGrip;
                    }
                    //VR Joystick                
                    if (JoyStickGrip > 0.75)
                    {
                        Quaternion PlaneRotDif = ControlsRoot.rotation * Quaternion.Inverse(PlaneRotLastFrame);//difference in plane's rotation since last frame
                        PlaneRotLastFrame = ControlsRoot.rotation;
                        JoystickZeroPoint = PlaneRotDif * JoystickZeroPoint;//zero point rotates with the plane so it appears still to the pilot
                        if (!JoystickGripLastFrame)//first frame you gripped joystick
                        {
                            EntityControl.SendEventToExtensions("SFEXT_O_JoystickGrabbed");
                            PlaneRotDif = Quaternion.identity;
                            if (SwitchHandsJoyThrottle)
                            { JoystickZeroPoint = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).rotation; }//rotation of the controller relative to the plane when it was pressed
                            else
                            { JoystickZeroPoint = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).rotation; }
                        }
                        //difference between the plane and the hand's rotation, and then the difference between that and the JoystickZeroPoint
                        Quaternion JoystickDifference;
                        if (SwitchHandsJoyThrottle)
                        { JoystickDifference = (Quaternion.Inverse(ControlsRoot.rotation) * localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).rotation) * Quaternion.Inverse(JoystickZeroPoint); }
                        else { JoystickDifference = (Quaternion.Inverse(ControlsRoot.rotation) * localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).rotation) * Quaternion.Inverse(JoystickZeroPoint); }

                        JoystickPosYaw = (JoystickDifference * ControlsRoot.forward);//angles to vector
                        JoystickPosYaw.y = 0;
                        JoystickPos = (JoystickDifference * ControlsRoot.up);
                        VRPitchRoll = new Vector2(JoystickPos.x, JoystickPos.z) * 1.41421f;

                        JoystickGripLastFrame = true;
                        //making a circular joy stick square
                        //pitch and roll
                        if (Mathf.Abs(VRPitchRoll.x) > Mathf.Abs(VRPitchRoll.y))
                        {
                            if (Mathf.Abs(VRPitchRoll.x) > 0)
                            {
                                float temp = VRPitchRoll.magnitude / Mathf.Abs(VRPitchRoll.x);
                                VRPitchRoll *= temp;
                            }
                        }
                        else if (Mathf.Abs(VRPitchRoll.y) > 0)
                        {
                            float temp = VRPitchRoll.magnitude / Mathf.Abs(VRPitchRoll.y);
                            VRPitchRoll *= temp;
                        }
                        //yaw
                        if (Mathf.Abs(JoystickPosYaw.x) > Mathf.Abs(JoystickPosYaw.z))
                        {
                            if (Mathf.Abs(JoystickPosYaw.x) > 0)
                            {
                                float temp = JoystickPosYaw.magnitude / Mathf.Abs(JoystickPosYaw.x);
                                JoystickPosYaw *= temp;
                            }
                        }
                        else if (Mathf.Abs(JoystickPosYaw.z) > 0)
                        {
                            float temp = JoystickPosYaw.magnitude / Mathf.Abs(JoystickPosYaw.z);
                            JoystickPosYaw *= temp;
                        }

                    }
                    else
                    {
                        JoystickPosYaw.x = 0;
                        VRPitchRoll = Vector3.zero;
                        if (JoystickGripLastFrame)//first frame you let go of joystick
                        { EntityControl.SendEventToExtensions("SFEXT_O_JoystickDropped"); }
                        JoystickGripLastFrame = false;
                    }

                    if (HasAfterburner)
                    {
                        if (AfterburnerOn)
                        { PlayerThrottle = Mathf.Clamp(PlayerThrottle + ((Shifti - LeftControli) * .5f * DeltaTime), 0, 1f); }
                        else
                        { PlayerThrottle = Mathf.Clamp(PlayerThrottle + ((Shifti - LeftControli) * .5f * DeltaTime), 0, ThrottleAfterburnerPoint); }
                    }
                    else
                    { PlayerThrottle = Mathf.Clamp(PlayerThrottle + ((Shifti - LeftControli) * .5f * DeltaTime), 0, 1f); }
                    //VR Throttle
                    if (ThrottleGrip > 0.75)
                    {
                        Vector3 handdistance;
                        if (SwitchHandsJoyThrottle)
                        { handdistance = ControlsRoot.position - localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position; }
                        else { handdistance = ControlsRoot.position - localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position; }
                        handdistance = ControlsRoot.InverseTransformDirection(handdistance);

                        float HandThrottleAxis = handdistance.z;

                        if (!ThrottleGripLastFrame)
                        {
                            EntityControl.SendEventToExtensions("SFEXT_O_ThrottleGrabbed");
                            ThrottleZeroPoint = HandThrottleAxis;
                            TempThrottle = PlayerThrottle;
                            HandDistanceZLastFrame = 0;
                        }
                        float ThrottleDifference = ThrottleZeroPoint - HandThrottleAxis;
                        ThrottleDifference *= ThrottleSensitivity;

                        //Detent function to prevent you going into afterburner by accident (bit of extra force required to turn on AB (actually hand speed))
                        if (((HandDistanceZLastFrame - HandThrottleAxis) * ThrottleSensitivity > .05f)/*detent overcome*/ && Fuel > LowFuel || ((PlayerThrottle > ThrottleAfterburnerPoint/*already in afterburner*/) || !HasAfterburner))
                        {
                            PlayerThrottle = Mathf.Clamp(TempThrottle + ThrottleDifference, 0, 1);
                        }
                        else
                        {
                            PlayerThrottle = Mathf.Clamp(TempThrottle + ThrottleDifference, 0, ThrottleAfterburnerPoint);
                        }
                        HandDistanceZLastFrame = HandThrottleAxis;
                        ThrottleGripLastFrame = true;
                    }
                    else
                    {
                        if (ThrottleGripLastFrame)
                        {
                            EntityControl.SendEventToExtensions("SFEXT_O_ThrottleDropped");
                        }
                        ThrottleGripLastFrame = false;
                    }

                    if (DisableTaxiRotation == 0 && Taxiing)
                    {
                        AngleOfAttack = 0;//prevent stall sound and aoavapor when on ground
                                          //rotate if trying to yaw
                        float TaxiingStillMulti = 1;
                        if (DisallowTaxiRotationWhileStill)
                        { TaxiingStillMulti = Mathf.Min(Speed * TaxiFullTurningSpeedDivider, 1); }
                        Taxiinglerper = Mathf.Lerp(Taxiinglerper, RotationInputs.y * TaxiRotationSpeed * Time.smoothDeltaTime * TaxiingStillMulti, TaxiRotationResponse * DeltaTime);
                        VehicleTransform.Rotate(Vector3.up, Taxiinglerper);

                        StillWindMulti = Mathf.Min(Speed * .1f, 1);
                    }
                    else
                    {
                        StillWindMulti = 1;
                        Taxiinglerper = 0;
                    }
                    //keyboard control for afterburner
                    if (Input.GetKeyDown(AfterBurnerKey) && HasAfterburner)
                    {
                        if (AfterburnerOn)
                            PlayerThrottle = ThrottleAfterburnerPoint;
                        else
                            PlayerThrottle = 1;
                    }
                    if (ThrottleOverridden > 0 && !ThrottleGripLastFrame)
                    {
                        ThrottleInput = PlayerThrottle = ThrottleOverride;
                    }
                    else//if cruise control disabled, use inputs
                    {
                        if (!InVR)
                        {
                            float LTrigger = 0;
                            float RTrigger = 0;
                            if (!InEditor)
                            {
                                LTrigger = Input.GetAxisRaw("Oculus_CrossPlatform_PrimaryIndexTrigger");
                                RTrigger = Input.GetAxisRaw("Oculus_CrossPlatform_SecondaryIndexTrigger");
                            }
                            if (LTrigger > .05f)//axis throttle input for people who wish to use it //.05 deadzone so it doesn't take effect for keyboard users with something plugged in
                            { ThrottleInput = LTrigger; }
                            else { ThrottleInput = PlayerThrottle; }
                        }
                        else { ThrottleInput = PlayerThrottle; }
                    }

                    Vector2 Throttles = UnpackThrottles(ThrottleInput);
                    Fuel = Mathf.Max(Fuel -
                                        ((Mathf.Max(Throttles.x, 0.25f) * FuelConsumption)
                                            + (Throttles.y * FuelConsumptionAB)) * DeltaTime, 0);


                    if (Fuel < LowFuel)
                    {
                        //max throttle scales down with amount of fuel below LowFuel
                        ThrottleInput = ThrottleInput * Fuel * LowFuelDivider;
                        if (!LowFuelLastFrame)
                        {
                            EntityControl.SendEventToExtensions("SFEXT_O_LowFuel");
                            LowFuelLastFrame = true;
                        }
                        if (Fuel == 0 && !NoFuelLastFrame)
                        {
                            NoFuelLastFrame = true;
                            EntityControl.SendEventToExtensions("SFEXT_O_NoFuel");
                        }
                    }

                    if (HasAfterburner)
                    {
                        if (ThrottleInput > ThrottleAfterburnerPoint && !AfterburnerOn)
                        {
                            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(SetAfterburnerOn));
                        }
                        else if (ThrottleInput <= ThrottleAfterburnerPoint && AfterburnerOn)
                        {
                            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(SetAfterburnerOff));
                        }
                    }
                    if (JoystickOverridden > 0 && !JoystickGripLastFrame)//joystick override enabled, and player not holding joystick
                    {
                        RotationInputs = JoystickOverride;
                    }
                    else//joystick override disabled, player has control
                    {
                        if (!InVR)
                        {
                            //allow stick flight in desktop mode
                            Vector2 LStickPos = new Vector2(0, 0);
                            Vector2 RStickPos = new Vector2(0, 0);
                            if (!InEditor)
                            {
                                LStickPos.x = Input.GetAxisRaw("Oculus_CrossPlatform_PrimaryThumbstickHorizontal");
                                LStickPos.y = Input.GetAxisRaw("Oculus_CrossPlatform_PrimaryThumbstickVertical");
                                RStickPos.x = Input.GetAxisRaw("Oculus_CrossPlatform_SecondaryThumbstickHorizontal");
                                RStickPos.y = Input.GetAxisRaw("Oculus_CrossPlatform_SecondaryThumbstickVertical");
                            }
                            VRPitchRoll = LStickPos;
                            JoystickPosYaw.x = RStickPos.x;
                            //make stick input square
                            if (Mathf.Abs(VRPitchRoll.x) > Mathf.Abs(VRPitchRoll.y))
                            {
                                if (Mathf.Abs(VRPitchRoll.x) > 0)
                                {
                                    float temp = VRPitchRoll.magnitude / Mathf.Abs(VRPitchRoll.x);
                                    VRPitchRoll *= temp;
                                }
                            }
                            else if (Mathf.Abs(VRPitchRoll.y) > 0)
                            {
                                float temp = VRPitchRoll.magnitude / Mathf.Abs(VRPitchRoll.y);
                                VRPitchRoll *= temp;
                            }
                        }

                        RotationInputs.x = Mathf.Clamp(VRPitchRoll.y + Wi + Si + downi + upi, -1, 1) * Limits;
                        RotationInputs.y = Mathf.Clamp(Qi + Ei + JoystickPosYaw.x, -1, 1) * Limits;
                        //roll isn't subject to flight limits
                        RotationInputs.z = Mathf.Clamp(((VRPitchRoll.x + Ai + Di + lefti + righti) * -1), -1, 1);
                    }

                    yaw = Mathf.Clamp(-RotationInputs.y, -1, 1) * YawStrength;
                    //wheel colliders are broken, this workaround stops the plane from being 'sticky' when you try to start moving it.
                    if (Speed < .2 && HasWheelColliders && ThrottleInput > 0)
                    {
                        if (ThrottleStrength < 0)
                        { VehicleRigidbody.velocity = VehicleTransform.forward * -.25f; }
                        else
                        { VehicleRigidbody.velocity = VehicleTransform.forward * .25f; }
                    }
                }
            }
            else
            {
                //brake is always on if the plane is on the ground
                if (Taxiing)
                {
                    StillWindMulti = Mathf.Min(Speed * .1f, 1);
                }
                else { StillWindMulti = 1; }
            }

            if (DisablePhysicsAndInputs == 0)
            {
                //Lerp the inputs for 'engine response', throttle decrease response is slower than increase (EngineSpoolDownSpeedMulti)
                if (EngineOutput < ThrottleInput)
                { EngineOutput = Mathf.Lerp(EngineOutput, ThrottleInput, AccelerationResponse * DeltaTime); }
                else
                { EngineOutput = Mathf.Lerp(EngineOutput, ThrottleInput, AccelerationResponse * EngineSpoolDownSpeedMulti * DeltaTime); }

                if (VehicleMoving)//optimization
                {
                    rotlift = Mathf.Min(AirSpeed / RotMultiMaxSpeed, 1);//using a simple linear curve for increasing control as you move faster

                    yaw *= Mathf.Max(YawThrustVecMulti, rotlift);

                    //Lerp the inputs for 'rotation response'
                    LerpedYaw = Mathf.Lerp(LerpedYaw, yaw, YawResponse * DeltaTime);
                }
                else
                {
                    VelLift = pitch = yaw = roll = 0;
                }
                if (Floating)
                {
                    Yawing = (VehicleTransform.right * LerpedYaw);
                    Vector2 Outputs = UnpackThrottles(EngineOutput);
                    Thrust = ThrustPoint.forward * (Mathf.Min(Outputs.x)//Throttle
                    * ThrottleStrength
                    + Mathf.Max(Outputs.y, 0)//Afterburner throttle
                    * ThrottleStrengthAB);
                }
                else
                {
                    Yawing = Vector3.zero;
                    Thrust = Vector3.zero;
                }
            }
        }
        else//non-owners need to know these values
        {
            Speed = AirSpeed = CurrentVel.magnitude;//wind speed is local anyway, so just use ground speed for non-owners
            rotlift = Mathf.Min(Speed / RotMultiMaxSpeed, 1);//so passengers can hear the airbrake
                                                             //AirVel = VehicleRigidbody.velocity - Wind;//wind isn't synced so this will be wrong
                                                             //AirSpeed = AirVel.magnitude;
        }
    }
    private void FixedUpdate()
    {
        if (IsOwner)
        {
            float DeltaTime = Time.fixedDeltaTime;
            //lerp velocity toward 0 to simulate air friction
            Vector3 VehicleVel = VehicleRigidbody.velocity;
            VehicleRigidbody.velocity = Vector3.Lerp(VehicleVel, FinalWind * StillWindMulti, ((((AirFriction) * ExtraDrag)) * 90) * DeltaTime);
            //apply thrust
            VehicleRigidbody.AddForceAtPosition(Thrust, ThrustPoint.position, ForceMode.Force);//deltatime is built into ForceMode.Force
            //apply yawing using yaw moment
            VehicleRigidbody.AddForceAtPosition(Yawing, YawMoment.position, ForceMode.Force);
            //calc Gs
            float gravity = 9.81f * DeltaTime;
            LastFrameVel.y -= gravity; //add gravity
            AllGs = Vector3.Distance(LastFrameVel, VehicleVel) / gravity;
            GDamageToTake += Mathf.Max((AllGs - MaxGs), 0);

            Vector3 Gs3 = VehicleTransform.InverseTransformDirection(VehicleVel - LastFrameVel);
            VertGs = Gs3.y / gravity;
            LastFrameVel = VehicleVel;
        }
    }
    public void Explode()//all the things players see happen when the vehicle explodes
    {
        EntityControl.dead = true;
        PlayerThrottle = 0;
        ThrottleInput = 0;
        EngineOutput = 0;
        if (HasAfterburner) { SetAfterburnerOff(); }
        Fuel = FullFuel;
        Yawing = Vector3.zero;

        EntityControl.SendEventToExtensions("SFEXT_G_Explode");

        SendCustomEventDelayedSeconds(nameof(ReAppear), RespawnDelay);
        SendCustomEventDelayedSeconds(nameof(NotDead), RespawnDelay + InvincibleAfterSpawn);

        if (IsOwner)
        {
            VehicleRigidbody.velocity = Vector3.zero;
            VehicleRigidbody.angularVelocity = Vector3.zero;
            VehicleRigidbody.drag = 9999;
            VehicleRigidbody.angularDrag = 9999;
            Health = FullHealth;//turns off low health smoke
            Fuel = FullFuel;
            AngleOfAttack = 0;
            VelLift = VelLiftStart;
            SendCustomEventDelayedSeconds("MoveToSpawn", RespawnDelay - 3);
            EntityControl.SendEventToExtensions("SFEXT_O_Explode");
        }

        //pilot and passengers are dropped out of the plane
        if ((Piloting || Passenger) && !InEditor)
        {
            EntityControl.ExitStation();
        }
    }
    public void SFEXT_O_OnPlayerJoined()
    {
        if (GroundedLastFrame)
        {
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(TouchDown));
        }
        if (FloatingLastFrame)
        {
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(TouchDownWater));
        }
    }
    public void ReAppear()
    {
        EntityControl.SendEventToExtensions("SFEXT_G_ReAppear");
        if (IsOwner)
        {
            VehicleRigidbody.drag = 0;
            VehicleRigidbody.angularDrag = 0;
        }
    }
    public void NotDead()
    {
        Health = FullHealth;
        EntityControl.dead = false;
    }
    public void MoveToSpawn()
    {
        PlayerThrottle = 0;//for editor test mode
        EngineOutput = 0;//^
        Health = FullHealth;
        if (InEditor || UsingManualSync)
        {
            VehicleTransform.SetPositionAndRotation(Spawnposition, Spawnrotation);
        }
        else
        {
            VehicleObjectSync.Respawn();
        }
        EntityControl.SendEventToExtensions("SFEXT_O_MoveToSpawn");
    }
    public void TouchDown()
    {
        //Debug.Log("TouchDown");
        if (GroundedLastFrame) { return; }
        GroundedLastFrame = true;
        Taxiing = true;
        EntityControl.SendEventToExtensions("SFEXT_G_TouchDown");
    }
    public void TouchDownWater()
    {
        //Debug.Log("TouchDownWater");
        if (FloatingLastFrame) { return; }
        FloatingLastFrame = true;
        Taxiing = true;
        EntityControl.SendEventToExtensions("SFEXT_G_TouchDownWater");
    }
    public void TakeOff()
    {
        //Debug.Log("TakeOff");
        Taxiing = false;
        FloatingLastFrame = false;
        GroundedLastFrame = false;
        EntityControl.SendEventToExtensions("SFEXT_G_TakeOff");
    }
    public void SetAfterburnerOn()
    {
        AfterburnerOn = true;
        EntityControl.SendEventToExtensions("SFEXT_G_AfterburnerOn");
    }
    public void SetAfterburnerOff()
    {
        AfterburnerOn = false;
        EntityControl.SendEventToExtensions("SFEXT_G_AfterburnerOff");
    }
    private void ToggleAfterburner()
    {
        if (!AfterburnerOn && ThrottleInput > ThrottleAfterburnerPoint && Fuel > LowFuel)
        {
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(SetAfterburnerOn));
        }
        else if (AfterburnerOn)
        {
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(SetAfterburnerOff));
        }
    }
    public void SFEXT_O_ReSupply()
    {
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(ReSupply));
    }
    public void ReSupply()
    {
        ReSupplied = 0;//used to know if other scripts resupplied
        if ((Fuel < FullFuel - 10 || Health != FullHealth))
        {
            ReSupplied++;//used to only play the sound if we're actually repairing/getting ammo/fuel
        }
        EntityControl.SendEventToExtensions("SFEXT_G_ReSupply");//extensions increase the ReSupplied value too

        LastResupplyTime = Time.time;

        if (IsOwner)
        {
            Fuel = Mathf.Min(Fuel + (FullFuel / RefuelTime), FullFuel);
            Health = Mathf.Min(Health + (FullHealth / RepairTime), FullHealth);
            if (LowFuelLastFrame && Fuel > LowFuel)
            {
                LowFuelLastFrame = false;
                EntityControl.SendEventToExtensions("SFEXT_O_NotLowFuel");
            }
            if (NoFuelLastFrame && Fuel > 0)
            {
                NoFuelLastFrame = false;
                EntityControl.SendEventToExtensions("SFEXT_O_NotNoFuel");
            }
        }
    }
    public void SFEXT_O_RespawnButton()//called when using respawn button
    {
        if (!Occupied && !EntityControl.dead)
        {
            Networking.SetOwner(localPlayer, EntityControl.gameObject);
            EntityControl.TakeOwnerShipOfExtensions();
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "ResetStatus");
            IsOwner = true;
            //synced variables
            Health = FullHealth;
            Fuel = FullFuel;
            if (InEditor || UsingManualSync)
            {
                VehicleTransform.SetPositionAndRotation(Spawnposition, Spawnrotation);
                VehicleRigidbody.velocity = Vector3.zero;
            }
            else
            {
                VehicleObjectSync.Respawn();
            }
            VehicleRigidbody.angularVelocity = Vector3.zero;//editor needs this
        }
    }
    public void ResetStatus()//called globally when using respawn button
    {
        if (HasAfterburner) { SetAfterburnerOff(); }
        //these two make it invincible and unable to be respawned again for 5s
        EntityControl.dead = true;
        SendCustomEventDelayedSeconds(nameof(NotDead), InvincibleAfterSpawn);
        EntityControl.SendEventToExtensions("SFEXT_G_RespawnButton");
    }
    public void SendBulletHit()
    {
        EntityControl.SendEventToExtensions("SFEXT_G_BulletHit");
    }
    public void SFEXT_L_BulletHit()
    {
        if (PredictDamage)
        {
            if (Time.time - LastHitTime > 2)
            {
                PredictedHealth = Health - BulletDamageTaken;
                if (PredictedHealth <= 0)
                {
                    SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(Explode));
                }
            }
            else
            {
                PredictedHealth -= BulletDamageTaken;
                if (PredictedHealth <= 0)
                {
                    SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(Explode));
                }
            }
            LastHitTime = Time.time;
        }
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(SendBulletHit));
    }
    public void SFEXT_G_BulletHit()
    {
        if (!EntityControl.dead)
        {
            LastHitTime = Time.time;
            if (IsOwner)
            {
                Health -= BulletDamageTaken;
                if (PredictDamage && Health <= 0)//the attacker calls the explode function in this case
                {
                    Health = 0.0911f;
                    //if two people attacked us, and neither predicted they killed us but we took enough damage to die, we must still die.
                    SendCustomEventDelayedSeconds(nameof(CheckLaggyKilled), .25f);//give enough time for the explode event to happen if they did predict we died, otherwise do it ourself
                }
            }
        }
    }
    public void CheckLaggyKilled()
    {
        if (!EntityControl.dead)
        {
            //Check if we still have the amount of health set to not send explode when killed, and if we do send explode
            if (Health == 0.0911f)
            {
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(Explode));
            }
        }
    }
    public void SFEXT_P_PassengerEnter()
    {
        Passenger = true;
        SetCollidersLayer(OnboardVehicleLayer);
    }
    public void SFEXT_P_PassengerExit()
    {
        Passenger = false;
        localPlayer.SetVelocity(CurrentVel);
        SetCollidersLayer(VehicleLayer);
    }
    public void SFEXT_O_TakeOwnership()
    {
        IsOwner = true;
        VehicleRigidbody.velocity = CurrentVel;
        VehicleRigidbody.drag = 0;
        VehicleRigidbody.angularDrag = 0;
    }
    public void SFEXT_O_LoseOwnership()
    {
        IsOwner = false;
        VehicleRigidbody.drag = 9999;
        VehicleRigidbody.angularDrag = 9999;
    }
    public void SFEXT_O_PilotEnter()
    {
        //setting this as a workaround because it doesnt work reliably in Start()
        if (!InEditor)
        {
            InVR = localPlayer.IsUserInVR();//move me to start when they fix the bug
                                            //https://feedback.vrchat.com/vrchat-udon-closed-alpha-bugs/p/vrcplayerapiisuserinvr-for-the-local-player-is-not-returned-correctly-when-calle
        }

        EngineOutput = 0;
        ThrottleInput = 0;
        PlayerThrottle = 0;
        GDHitRigidbody = null;

        Piloting = true;
        if (EntityControl.dead) { Health = FullHealth; }//dead is true for the first 5 seconds after spawn, this might help with spontaneous explosions

        //hopefully prevents explosions when you enter the plane
        VehicleRigidbody.velocity = CurrentVel;
        VertGs = 0;
        AllGs = 0;
        LastFrameVel = CurrentVel;

        SetCollidersLayer(OnboardVehicleLayer);
    }
    public void SFEXT_G_PilotEnter()
    {
        Occupied = true;
        EntityControl.dead = false;//Plane stops being invincible if someone gets in, also acts as redundancy incase someone missed the notdead event
    }
    public void SFEXT_G_PilotExit()
    {
        Occupied = false;
        SetAfterburnerOff();
    }
    public void SFEXT_O_PilotExit()
    {
        //zero control values
        roll = 0;
        pitch = 0;
        yaw = 0;
        LerpedYaw = 0;
        RotationInputs = Vector3.zero;
        ThrottleInput = 0;
        //reset everything
        Piloting = false;
        Taxiinglerper = 0;
        ThrottleGripLastFrame = false;
        JoystickGripLastFrame = false;
        DoAAMTargeting = false;
        Yawing = Vector3.zero;
        localPlayer.SetVelocity(CurrentVel);

        //set vehicle's collider's layers back
        SetCollidersLayer(VehicleLayer);
    }
    public void SetCollidersLayer(int NewLayer)
    {
        if (VehicleMesh)
        {
            Transform[] children = VehicleMesh.GetComponentsInChildren<Transform>();
            foreach (Transform child in children)
            {
                child.gameObject.layer = NewLayer;
            }
        }
    }
    private void WindAndAoA()
    {
        if (DisablePhysicsAndInputs != 0) { return; }
        float TimeGustiness = Time.time * WindGustiness;
        float gustx = TimeGustiness + (VehicleTransform.position.x * WindTurbulanceScale);
        float gustz = TimeGustiness + (VehicleTransform.position.z * WindTurbulanceScale);
        FinalWind = Vector3.Normalize(new Vector3((Mathf.PerlinNoise(gustx + 9000, gustz) - .5f), /* (Mathf.PerlinNoise(gustx - 9000, gustz - 9000) - .5f) */0, (Mathf.PerlinNoise(gustx, gustz + 9999) - .5f))) * WindGustStrength;
        FinalWind = (FinalWind + Wind);
        AirVel = VehicleRigidbody.velocity - (FinalWind * StillWindMulti);
        AirSpeed = AirVel.magnitude;
        Vector3 VecForward = VehicleTransform.forward;
    }
    public Vector2 UnpackThrottles(float Throttle)
    {
        //x = throttle amount (0-1), y = afterburner amount (0-1)
        return new Vector2(Mathf.Min(Throttle, ThrottleAfterburnerPoint) * ThrottleNormalizer,
        Mathf.Max((Mathf.Max(Throttle, ThrottleAfterburnerPoint) - ThrottleAfterburnerPoint) * ABNormalizer, 0));
    }
}