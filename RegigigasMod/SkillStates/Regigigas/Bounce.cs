﻿using EntityStates;
using RoR2;
using UnityEngine;
using System;
using UnityEngine.Networking;
using RoR2.Projectile;
using UnityEngine.AddressableAssets;
using static RoR2.CameraTargetParams;

namespace RegigigasMod.SkillStates.Regigigas
{
    public class Bounce : BaseSkillState
    {
        public static float minDamageCoefficient = 8f;
        public static float maxDamageCoefficient = 36f;
        public static float leapDuration = 0.6f;

        private float duration;
        private bool hasLanded;
        private float stopwatch;
        private float previousAirControl;
        private GameObject chargeEffectInstanceL;
        private GameObject chargeEffectInstanceR;
        private bool isFalling;
        private bool setCamOverride;
        private float maxYSpeed;

        private CameraParamsOverrideHandle camParamsOverrideHandle;

        public override void OnEnter()
        {
            base.OnEnter();
            this.duration = Bounce.leapDuration / (0.75f + (0.25f * this.attackSpeedStat));
            this.hasLanded = false;
            base.characterMotor.jumpCount = base.characterBody.maxJumpCount;

            this.previousAirControl = base.characterMotor.airControl;
            base.characterMotor.airControl = EntityStates.Croco.Leap.airControl;

            Vector3 direction = base.GetAimRay().direction;

            if (base.isAuthority)
            {
                base.characterBody.isSprinting = true;

                direction.y = Mathf.Max(direction.y, 1.25f * EntityStates.Croco.Leap.minimumY);
                Vector3 a = direction.normalized * (1.15f * EntityStates.Croco.Leap.aimVelocity) * (3 + (0.5f * this.moveSpeedStat));
                Vector3 b = Vector3.up * 5f * EntityStates.Croco.Leap.upwardVelocity;
                Vector3 b2 = new Vector3(direction.x, 0f, direction.z).normalized * (0.75f * EntityStates.Croco.Leap.forwardVelocity);

                base.characterMotor.Motor.ForceUnground();
                base.characterMotor.velocity = a + b + b2;
            }

            this.characterDirection.moveVector = direction;

            this.characterBody.bodyFlags |= CharacterBody.BodyFlags.IgnoreFallDamage;

            Util.PlaySound("sfx_regigigas_leap", base.gameObject);

            this.chargeEffectInstanceL = GameObject.Instantiate(Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Grandparent/ChargeGrandParentSunHands.prefab").WaitForCompletion());
            this.chargeEffectInstanceL.transform.parent = this.FindModelChild("FootL");
            this.chargeEffectInstanceL.transform.localPosition = new Vector3(0f, 0f, 0f);
            this.chargeEffectInstanceL.transform.localRotation = Quaternion.identity;
            this.chargeEffectInstanceL.transform.localScale = Vector3.one;

            this.chargeEffectInstanceR = GameObject.Instantiate(Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Grandparent/ChargeGrandParentSunHands.prefab").WaitForCompletion());
            this.chargeEffectInstanceR.transform.parent = this.FindModelChild("FootR");
            this.chargeEffectInstanceR.transform.localPosition = new Vector3(0f, 0f, 0f);
            this.chargeEffectInstanceR.transform.localRotation = Quaternion.identity;
            this.chargeEffectInstanceR.transform.localScale = Vector3.one;

            EffectData effectData = new EffectData();
            effectData.origin = base.characterBody.footPosition;
            effectData.scale = 5f;

            EffectManager.SpawnEffect(Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Parent/ParentSlamEffect.prefab").WaitForCompletion(), effectData, true);
        }

        public override void OnExit()
        {
            base.OnExit();

            if (this.chargeEffectInstanceL) EntityState.Destroy(this.chargeEffectInstanceL);
            if (this.chargeEffectInstanceR) EntityState.Destroy(this.chargeEffectInstanceR);

            base.characterBody.bodyFlags &= ~CharacterBody.BodyFlags.IgnoreFallDamage;
            base.characterMotor.airControl = this.previousAirControl;

            if (this.setCamOverride) this.cameraTargetParams.RemoveParamsOverride(this.camParamsOverrideHandle, 1.5f);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            base.StartAimMode(0.5f, false);
            this.characterBody.isSprinting = true;
            this.stopwatch += Time.fixedDeltaTime;

            if (!this.isFalling)
            {
                if (this.characterMotor.velocity.y <= 0f && base.fixedAge >= 0.1f)
                {
                    this.isFalling = true;
                    this.setCamOverride = true;
                    this.camParamsOverrideHandle = Modules.CameraParams.OverrideCameraParams(base.cameraTargetParams, RegigigasCameraParams.CHARGE, 2f);
                }
            }

            // cache this and use your highest downward velocity to calc damage
            if (this.characterMotor.velocity.y <= this.maxYSpeed) this.maxYSpeed = this.characterMotor.velocity.y;

            if (this.isFalling) this.characterMotor.velocity.y += (-24f * Time.fixedDeltaTime);

            if (this.stopwatch >= this.duration && base.isAuthority && base.characterMotor.isGrounded)
            {
                this.GroundImpact();
                this.outer.SetNextStateToMain();
                return;
            }
        }

        private void GroundImpact()
        {
            if (!this.hasLanded)
            {
                this.hasLanded = true;

                Util.PlaySound("sfx_regigigas_slam", base.gameObject);
                //Util.PlaySound("Play_parent_attack1_slam", base.gameObject);

                float radius = 24f;

                EffectData effectData = new EffectData();
                effectData.origin = base.characterBody.footPosition;
                effectData.scale = radius;

                EffectManager.SpawnEffect(Modules.Assets.slamImpactEffect, effectData, true);

                float dmg = Util.Remap(this.maxYSpeed, 0f, -500f, Bounce.minDamageCoefficient, Bounce.maxDamageCoefficient);

                new BlastAttack
                {
                    attacker = this.gameObject,
                    attackerFiltering = AttackerFiltering.NeverHitSelf,
                    baseDamage = dmg * this.damageStat,
                    baseForce = 800f,
                    bonusForce = Vector3.up * 2000f,
                    crit = this.RollCrit(),
                    damageColorIndex = DamageColorIndex.Default,
                    damageType = DamageType.AOE,
                    falloffModel = BlastAttack.FalloffModel.None,
                    inflictor = this.gameObject,
                    losType = BlastAttack.LoSType.None,
                    position = this.characterBody.footPosition,
                    procChainMask = default(ProcChainMask),
                    procCoefficient = 1.0f,
                    radius = radius,
                    teamIndex = this.GetTeam()
                }.Fire();
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Pain;
        }
    }
}