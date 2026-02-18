using System;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Persistence.JsonUtility;
using Kingmaker.GameModes;
using Kingmaker.UI.PointMarker;
using UnityEngine;

namespace WOTRMultiplayer.UnityBehaviours.Ping
{
    public class WorldPositionPingOutsideOfCameraBehaviour : DecayingBehaviourBase
    {
        private PointMarker _marker;
        private FakeUnitPointMarkerData _fakeUnitData;
        private bool _isEnabled = false;

        public void Begin(TimeSpan expiration, Vector3 position)
        {
            _fakeUnitData.Position = position;
            _isEnabled = true;
            Begin(expiration);
        }

        public WorldPositionPingOutsideOfCameraBehaviour WithPortrait(Sprite markerSprite)
        {
            _marker.Portrait.sprite = markerSprite;
            return this;
        }

        protected override void OnExpired()
        {
            _marker.Hide();
            _isEnabled = false;
            base.OnExpired();
        }

        protected override void OnPartialDecay(float decayState)
        {
            if (!_isEnabled || (Game.Instance.CurrentMode != GameModeType.Default && Game.Instance.CurrentMode != GameModeType.Pause))
            {
                return;
            }

            var camera = Game.GetCamera();
            if (camera == null)
            {
                return;
            }

            Vector3 vector = camera.WorldToScreenPoint(_fakeUnitData.Position);
            if (vector.x <= 0f || vector.x >= camera.pixelWidth || vector.y <= 0f || vector.y >= camera.pixelHeight)
            {
                PointMarkerController.Instance.UpdateBorders();
                _marker.SetBorders(PointMarkerController.Instance.m_Borders);
                _marker.Show();
                return;
            }

            _marker.Hide();
        }

        private void Awake()
        {
            this.transform.SetParent(PointMarkerController.Instance.transform, false);

            _marker = GetComponent<PointMarker>();
            _fakeUnitData = new FakeUnitPointMarkerData();
            _marker.Init(_fakeUnitData, PointMarkerController.Instance.m_Borders);
        }

        private class FakeUnitPointMarkerData : UnitEntityData
        {
            public override Vector3 Position { get; set; }

            public FakeUnitPointMarkerData()
                : base((JsonConstructorMark)default)
            {
                // actual value of this.Portrait property is never used, just need to make sure it doesn't fail with NRE (PointMarker.Init)
                this.Descriptor = new Kingmaker.UnitLogic.UnitDescriptor(default);
                var settings = new Kingmaker.UI.UnitSettings.UnitUISettings(this.Descriptor)
                {
                    m_IgnoreOriginalPortrait = true,
                    m_OverridePortrait = new Kingmaker.Blueprints.BlueprintPortrait() { Data = new Kingmaker.Blueprints.PortraitData() }
                };
                var field = this.Descriptor.GetType().GetField(nameof(this.Descriptor.UISettings), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                field.SetValue(this.Descriptor, settings);
            }
        }
    }
}
