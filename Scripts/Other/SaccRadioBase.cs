
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

namespace SaccFlightAndVehicles
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SaccRadioBase : UdonSharpBehaviour
    {
        [Header("Vehicles must have SAV_Radio extension for this to work")]
        private SaccFlightAndVehicles.SaccEntity[] _AllPlanes_ENT;
        private SAV_Radio[] _AllPlanes_RD;
        public float VoiceNear = 199999;
        public float VoiceFar = 200000;
        // public float VoiceVolumetric = 1500;
        public float VoiceGain = .05f;
        // public float VoiceLowPass;
        [Tooltip("Make this text object darker when radio is disabled. Not required.")]
        public TextMeshProUGUI RadioEnabledTxt;
        public bool RadioEnabled = true;
        [Header("All Planes and RadioZones are filled automatically on build.")]
        public Transform[] AllPlanes;
        public SaccRadioZone[] RadioZones;
        [Header("Debug, leave empty:")]
        public SaccFlightAndVehicles.SaccEntity MyVehicle;
        [System.NonSerialized] public SaccRadioZone MyZone;
        private int NextPlane;
        private int NextZone;
        private int NumZones;
        private bool DoZones = false;
        void Start()
        {
            SendCustomEventDelayedSeconds(nameof(SetRadioVoiceVolumes), 5);
            _AllPlanes_ENT = new SaccFlightAndVehicles.SaccEntity[AllPlanes.Length];
            _AllPlanes_RD = new SAV_Radio[AllPlanes.Length];
            for (int i = 0; i < AllPlanes.Length; i++)
            {
                _AllPlanes_ENT[i] = (SaccFlightAndVehicles.SaccEntity)AllPlanes[i].GetComponent<SaccFlightAndVehicles.SaccEntity>();
                if (_AllPlanes_ENT[i]) { _AllPlanes_RD[i] = (SAV_Radio)_AllPlanes_ENT[i].GetExtention("SAV_Radio"); }
            }
            NumZones = RadioZones.Length;
            if (NumZones != 0) { DoZones = true; }
        }
        public void SetRadioVoiceVolumes()
        {
            SendCustomEventDelayedFrames(nameof(SetRadioVoiceVolumes), 5);
            if ((!MyVehicle || !RadioEnabled) && !MyZone) { return; }
            NextPlane++;
            if (NextPlane == _AllPlanes_RD.Length) { NextPlane = 0; }
            if (_AllPlanes_RD[NextPlane])
            {
                if (!_AllPlanes_RD[NextPlane].RadioOn || MyVehicle == _AllPlanes_ENT[NextPlane]) { return; }
                for (int o = 0; o < _AllPlanes_ENT[NextPlane].VehicleSeats.Length; o++)
                {
                    VRCPlayerApi thisplayer = _AllPlanes_ENT[NextPlane].VehicleSeats[o].SeatedPlayer;
                    if (thisplayer != null)
                    {
                        thisplayer.SetVoiceDistanceNear(VoiceNear);
                        thisplayer.SetVoiceDistanceFar(VoiceFar);
                        thisplayer.SetVoiceGain(VoiceGain);
                    }
                }
            }
            if (DoZones)
            {
                SendCustomEventDelayedFrames(nameof(SetRadioVoiceVolumes_Zones), 2);//separate in frames for optimization
            }
        }
        public void SetRadioVoiceVolumes_Zones()
        {
            NextZone++;
            if (NextZone >= NumZones) { NextZone = 0; }
            SaccRadioZone NextRZ = RadioZones[NextZone];
            VRCPlayerApi[] RZ_players = NextRZ.playersinside;
            if (NextRZ != MyZone)
            {
                for (int i = 0; i < NextRZ.numPlayersInside; i++)
                {
                    RZ_players[i].SetVoiceDistanceNear(VoiceNear);
                    RZ_players[i].SetVoiceDistanceFar(VoiceFar);
                    RZ_players[i].SetVoiceGain(VoiceGain);
                }
            }
        }
        public void SetRadioVoiceVolumesDefault()
        {
            for (int i = 0; i < _AllPlanes_RD.Length; i++)
            {
                if (_AllPlanes_RD[i])
                {
                    for (int o = 0; o < _AllPlanes_ENT[i].VehicleSeats.Length; o++)
                    {
                        VRCPlayerApi thisplayer = _AllPlanes_ENT[i].VehicleSeats[o].SeatedPlayer;
                        if (thisplayer != null)
                        {
                            thisplayer.SetVoiceDistanceNear(0);
                            thisplayer.SetVoiceDistanceFar(25);
                            thisplayer.SetVoiceGain(15);
                        }
                    }
                }
            }
        }
        public void ToggleRadio()
        {
            RadioEnabled = !RadioEnabled;
            if (RadioEnabledTxt) RadioEnabledTxt.color = RadioEnabled ? Color.white : Color.gray;
        }
    }
}