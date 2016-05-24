#region Header
// **********
// ServUO - BaseWeapon.cs
// **********
#endregion

#region References
using System;
using System.Collections.Generic;

using Server.ContextMenus;
using Server.Engines.Craft;
using Server.Engines.XmlSpawner2;
using Server.Ethics;
using Server.Factions;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.SkillHandlers;
using Server.Spells;
using Server.Spells.Sixth;
#endregion

namespace Server.Items
{
	public interface ISlayer
	{
		SlayerName Slayer { get; set; }
		SlayerName Slayer2 { get; set; }
	}

	public abstract class BaseWeapon : Item, IWeapon, IFactionItem, ICraftable, ISlayer, IDurability
	{
		private string m_EngravedText;

		[CommandProperty(AccessLevel.GameMaster)]
		public string EngravedText
		{
			get { return m_EngravedText; }
			set
			{
				m_EngravedText = value;
				InvalidateProperties();
			}
		}

		#region Factions
		private FactionItem m_FactionState;

		public FactionItem FactionItemState
		{
			get { return m_FactionState; }
			set
			{
				m_FactionState = value;

				if (m_FactionState == null)
				{
					Hue = CraftResources.GetHue(Resource);
				}

				LootType = (m_FactionState == null ? LootType.Regular : LootType.Blessed);
			}
		}
		#endregion

		/* Weapon internals work differently now (Mar 13 2003)
        * 
        * The attributes defined below default to -1.
        * If the value is -1, the corresponding virtual 'Aos/Old' property is used.
        * If not, the attribute value itself is used. Here's the list:
        *  - MinDamage
        *  - MaxDamage
        *  - Speed
        *  - HitSound
        *  - MissSound
        *  - StrRequirement, DexRequirement, IntRequirement
        *  - WeaponType
        *  - WeaponAnimation
        *  - MaxRange
        */

		// Instance values. These values are unique to each weapon.
		private WeaponDamageLevel m_DamageLevel;
		private WeaponAccuracyLevel m_AccuracyLevel;
		private WeaponDurabilityLevel m_DurabilityLevel;
		private WeaponQuality m_Quality;
		private Mobile m_Crafter;
		private Poison m_Poison;
		private int m_PoisonCharges;
		private bool m_Identified;
		private int m_Hits;
		private int m_MaxHits;
		private SlayerName m_Slayer;
		private SlayerName m_Slayer2;


		private SkillMod m_SkillMod, m_MageMod;
		private CraftResource m_Resource;
		private bool m_PlayerConstructed;

		// Overridable values. These values are provided to override the defaults which get defined in the individual weapon scripts.
		private int m_StrReq, m_DexReq, m_IntReq;
		private int m_MinDamage, m_MaxDamage;
		private int m_HitSound, m_MissSound;
		private float m_Speed;
		private int m_MaxRange;
		private SkillName m_Skill;
		private WeaponType m_Type;
		private WeaponAnimation m_Animation;

        #region Virtual Properties
        public virtual WeaponAbility PrimaryAbility { get { return null; } }
		public virtual WeaponAbility SecondaryAbility { get { return null; } }

		public virtual int DefMaxRange { get { return 1; } }
		public virtual int DefHitSound { get { return 0; } }
		public virtual int DefMissSound { get { return 0; } }
		public virtual SkillName DefSkill { get { return SkillName.Swords; } }
		public virtual WeaponType DefType { get { return WeaponType.Slashing; } }
		public virtual WeaponAnimation DefAnimation { get { return WeaponAnimation.Slash1H; } }

		public virtual int AosStrengthReq { get { return 0; } }
		public virtual int AosDexterityReq { get { return 0; } }
		public virtual int AosIntelligenceReq { get { return 0; } }
		public virtual int AosMinDamage { get { return 0; } }
		public virtual int AosMaxDamage { get { return 0; } }
		public virtual int AosSpeed { get { return 0; } }
		public virtual float MlSpeed { get { return 0.0f; } }
		public virtual int AosMaxRange { get { return DefMaxRange; } }
		public virtual int AosHitSound { get { return DefHitSound; } }
		public virtual int AosMissSound { get { return DefMissSound; } }
		public virtual SkillName AosSkill { get { return DefSkill; } }
		public virtual WeaponType AosType { get { return DefType; } }
		public virtual WeaponAnimation AosAnimation { get { return DefAnimation; } }

		public virtual int OldStrengthReq { get { return 0; } }
		public virtual int OldDexterityReq { get { return 0; } }
		public virtual int OldIntelligenceReq { get { return 0; } }
		public virtual int OldMinDamage { get { return 0; } }
		public virtual int OldMaxDamage { get { return 0; } }
		public virtual int OldSpeed { get { return 0; } }
		public virtual int OldMaxRange { get { return DefMaxRange; } }
		public virtual int OldHitSound { get { return DefHitSound; } }
		public virtual int OldMissSound { get { return DefMissSound; } }
		public virtual SkillName OldSkill { get { return DefSkill; } }
		public virtual WeaponType OldType { get { return DefType; } }
		public virtual WeaponAnimation OldAnimation { get { return DefAnimation; } }

		public virtual int InitMinHits { get { return 0; } }
		public virtual int InitMaxHits { get { return 0; } }

		public virtual SkillName AccuracySkill { get { return SkillName.Tactics; } }

        public override double DefaultWeight
        {
            get
            {
                return base.DefaultWeight * 3;
            }
        }

		#region Personal Bless Deed
		private Mobile m_BlessedBy;

		[CommandProperty(AccessLevel.GameMaster)]
		public Mobile BlessedBy
		{
			get { return m_BlessedBy; }
			set
			{
				m_BlessedBy = value;
				InvalidateProperties();
			}
		}

		private class UnBlessEntry : ContextMenuEntry
		{
			private readonly Mobile m_From;
			private readonly BaseWeapon m_Weapon; // BaseArmor, BaseWeapon or BaseClothing

			public UnBlessEntry(Mobile from, BaseWeapon weapon)
				: base(6208, -1)
			{
				m_From = from;
				m_Weapon = weapon;
			}

			public override void OnClick()
			{
				m_Weapon.BlessedFor = null;
				m_Weapon.BlessedBy = null;

				Container pack = m_From.Backpack;

				if (pack != null)
				{
					pack.DropItem(new PersonalBlessDeed(m_From));
					m_From.SendLocalizedMessage(1062200); // A personal bless deed has been placed in your backpack.
				}
			}
		}
		#endregion

		#endregion

		#region Getters & Setters
		[CommandProperty(AccessLevel.GameMaster)]
		public bool Identified
		{
			get { return m_Identified; }
			set
			{
				m_Identified = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int HitPoints
		{
			get { return m_Hits; }
			set
			{
				if (m_Hits == value)
				{
					return;
				}

				if (value > m_MaxHits)
				{
					value = m_MaxHits;
				}

				m_Hits = value;

				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int MaxHitPoints
		{
			get { return m_MaxHits; }
			set
			{
				m_MaxHits = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int PoisonCharges
		{
			get { return m_PoisonCharges; }
			set
			{
				m_PoisonCharges = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public Poison Poison
		{
			get { return m_Poison; }
			set
			{
				m_Poison = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public WeaponQuality Quality
		{
			get { return m_Quality; }
			set
			{
				UnscaleDurability();
				m_Quality = value;
				ScaleDurability();
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public Mobile Crafter
		{
			get { return m_Crafter; }
			set
			{
				m_Crafter = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public SlayerName Slayer
		{
			get { return m_Slayer; }
			set
			{
				m_Slayer = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public SlayerName Slayer2
		{
			get { return m_Slayer2; }
			set
			{
				m_Slayer2 = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public CraftResource Resource
		{
			get { return m_Resource; }
			set
			{
				UnscaleDurability();
				m_Resource = value;
				Hue = CraftResources.GetHue(m_Resource);
				InvalidateProperties();
				ScaleDurability();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public WeaponDamageLevel DamageLevel
		{
			get { return m_DamageLevel; }
			set
			{
				m_DamageLevel = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public WeaponDurabilityLevel DurabilityLevel
		{
			get { return m_DurabilityLevel; }
			set
			{
				UnscaleDurability();
				m_DurabilityLevel = value;
				InvalidateProperties();
				ScaleDurability();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public bool PlayerConstructed { get { return m_PlayerConstructed; } set { m_PlayerConstructed = value; } }

		[CommandProperty(AccessLevel.GameMaster)]
		public int MaxRange
		{
			get { return (m_MaxRange == -1 ? Core.AOS ? AosMaxRange : OldMaxRange : m_MaxRange); }
			set
			{
				m_MaxRange = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public WeaponAnimation Animation { get { return (m_Animation == (WeaponAnimation)(-1) ? Core.AOS ? AosAnimation : OldAnimation : m_Animation); } set { m_Animation = value; } }

		[CommandProperty(AccessLevel.GameMaster)]
		public WeaponType Type { get { return (m_Type == (WeaponType)(-1) ? Core.AOS ? AosType : OldType : m_Type); } set { m_Type = value; } }

		[CommandProperty(AccessLevel.GameMaster)]
		public SkillName Skill
		{
			get { return (m_Skill == (SkillName)(-1) ? Core.AOS ? AosSkill : OldSkill : m_Skill); }
			set
			{
				m_Skill = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int HitSound { get { return (m_HitSound == -1 ? Core.AOS ? AosHitSound : OldHitSound : m_HitSound); } set { m_HitSound = value; } }

		[CommandProperty(AccessLevel.GameMaster)]
		public int MissSound { get { return (m_MissSound == -1 ? Core.AOS ? AosMissSound : OldMissSound : m_MissSound); } set { m_MissSound = value; } }

		[CommandProperty(AccessLevel.GameMaster)]
		public int MinDamage
		{
			get { return (m_MinDamage == -1 ? Core.AOS ? AosMinDamage : OldMinDamage : m_MinDamage); }
			set
			{
				m_MinDamage = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int MaxDamage
		{
			get { return (m_MaxDamage == -1 ? Core.AOS ? AosMaxDamage : OldMaxDamage : m_MaxDamage); }
			set
			{
				m_MaxDamage = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public float Speed
		{
			get
			{
				if (m_Speed != -1)
				{
					return m_Speed;
				}

				if (Core.ML)
				{
					return MlSpeed;
				}
				else if (Core.AOS)
				{
					return AosSpeed;
				}

				return OldSpeed;
			}
			set
			{
				m_Speed = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int StrRequirement
		{
            get { return Math.Min(110, (int)((double)(m_StrReq == -1 ? Core.AOS ? AosStrengthReq : OldStrengthReq : m_StrReq))); }
			set
			{
				m_StrReq = value;
				InvalidateProperties();
			}
		}

		[CommandProperty(AccessLevel.GameMaster)]
		public int DexRequirement { get { return (m_DexReq == -1 ? Core.AOS ? AosDexterityReq : OldDexterityReq : m_DexReq); } set { m_DexReq = value; } }

		[CommandProperty(AccessLevel.GameMaster)]
		public int IntRequirement { get { return (m_IntReq == -1 ? Core.AOS ? AosIntelligenceReq : OldIntelligenceReq : m_IntReq); } set { m_IntReq = value; } }

		[CommandProperty(AccessLevel.GameMaster)]
		public WeaponAccuracyLevel AccuracyLevel
		{
			get { return m_AccuracyLevel; }
			set
			{
				if (m_AccuracyLevel != value)
				{
					m_AccuracyLevel = value;

					if (UseSkillMod)
					{
						if (m_AccuracyLevel == WeaponAccuracyLevel.Regular)
						{
							if (m_SkillMod != null)
							{
								m_SkillMod.Remove();
							}

							m_SkillMod = null;
						}
						else if (m_SkillMod == null && Parent is Mobile)
						{
							m_SkillMod = new DefaultSkillMod(AccuracySkill, true, (int)m_AccuracyLevel * 5);
							((Mobile)Parent).AddSkillMod(m_SkillMod);
						}
						else if (m_SkillMod != null)
						{
							m_SkillMod.Value = (int)m_AccuracyLevel * 5;
						}
					}

					InvalidateProperties();
				}
			}
		}

        public Mobile FocusWeilder { get; set; }
        public Mobile EnchantedWeilder { get; set; }

        public double ConsecrateProcChance { get; set; }
        public double ConsecrateDamageBonus { get; set; }
        #endregion

        public override void GetContextMenuEntries(Mobile from, List<ContextMenuEntry> list)
		{
			base.GetContextMenuEntries(from, list);

			if (BlessedFor == from && BlessedBy == from && RootParent == from)
			{
				list.Add(new UnBlessEntry(from, this));
			}

			XmlLevelItem levitem = XmlAttach.FindAttachment(this, typeof(XmlLevelItem)) as XmlLevelItem;

			if (levitem != null)
			{
				list.Add(new LevelInfoEntry(from, this, AttributeCategory.Melee));
			}
		}

		public override void OnAfterDuped(Item newItem)
		{
			BaseWeapon weap = newItem as BaseWeapon;

			if (weap == null)
			{
				return;
			}
		}

		public virtual void UnscaleDurability()
		{
			int scale = 100 + GetDurabilityBonus();

			m_Hits = ((m_Hits * 100) + (scale - 1)) / scale;
			m_MaxHits = ((m_MaxHits * 100) + (scale - 1)) / scale;
			InvalidateProperties();
		}

		public virtual void ScaleDurability()
		{
			int scale = 100 + GetDurabilityBonus();

			m_Hits = ((m_Hits * scale) + 99) / 100;
			m_MaxHits = ((m_MaxHits * scale) + 99) / 100;
			InvalidateProperties();
		}

		public int GetDurabilityBonus()
		{
			int bonus = 0;

			if (m_Quality == WeaponQuality.Exceptional)
			{
				bonus += 20;
			}

			switch (m_DurabilityLevel)
			{
				case WeaponDurabilityLevel.Durable:
					bonus += 20;
					break;
				case WeaponDurabilityLevel.Substantial:
					bonus += 50;
					break;
				case WeaponDurabilityLevel.Massive:
					bonus += 70;
					break;
				case WeaponDurabilityLevel.Fortified:
					bonus += 100;
					break;
				case WeaponDurabilityLevel.Indestructible:
					bonus += 120;
					break;
			}

			return bonus;
		}

		public static void BlockEquip(Mobile m, TimeSpan duration)
		{
			if (m.BeginAction(typeof(BaseWeapon)))
			{
				new ResetEquipTimer(m, duration).Start();
			}
		}

		private class ResetEquipTimer : Timer
		{
			private readonly Mobile m_Mobile;

			public ResetEquipTimer(Mobile m, TimeSpan duration)
				: base(duration)
			{
				m_Mobile = m;
			}

			protected override void OnTick()
			{
				m_Mobile.EndAction(typeof(BaseWeapon));
			}
		}

		public override bool CheckConflictingLayer(Mobile m, Item item, Layer layer)
		{
			if (base.CheckConflictingLayer(m, item, layer))
			{
				return true;
			}

			if (Layer == Layer.TwoHanded && layer == Layer.OneHanded)
			{
				m.SendLocalizedMessage(500214); // You already have something in both hands.
				return true;
			}
			else if (Layer == Layer.OneHanded && layer == Layer.TwoHanded && !(item is BaseShield) && !(item is BaseEquipableLight))
			{
				m.SendLocalizedMessage(500215); // You can only wield one weapon at a time.
				return true;
			}

			return false;
		}

		public override bool AllowSecureTrade(Mobile from, Mobile to, Mobile newOwner, bool accepted)
		{
			if (!Ethic.CheckTrade(from, to, newOwner, this))
			{
				return false;
			}

			return base.AllowSecureTrade(from, to, newOwner, accepted);
		}

		public virtual Race RequiredRace { get { return null; } }
		//On OSI, there are no weapons with race requirements, this is for custom stuff

		#region SA
		public virtual bool CanBeWornByGargoyles { get { return false; } }
		#endregion

		public override bool CanEquip(Mobile from)
		{
			if (!Ethic.CheckEquip(from, this))
			{
				return false;
			}

			if (from.Dex < DexRequirement)
			{
				from.SendMessage("You are not nimble enough to equip that.");
				return false;
			}
			else if (from.Str < StrRequirement)
			{
				from.SendLocalizedMessage(500213); // You are not strong enough to equip that.
				return false;
			}
			else if (from.Int < IntRequirement)
			{
				from.SendMessage("You are not smart enough to equip that.");
				return false;
			}
			else if (!from.CanBeginAction(typeof(BaseWeapon)))
			{
				return false;
			}
				#region Personal Bless Deed
			else if (BlessedBy != null && BlessedBy != from)
			{
				from.SendLocalizedMessage(1075277); // That item is blessed by another player.

				return false;
			}
			else if (!XmlAttach.CheckCanEquip(this, from))
			{
				return false;
			}
				#endregion

			else
			{
				return base.CanEquip(from);
			}
		}

		public virtual bool UseSkillMod { get { return !Core.AOS; } }

		public override bool OnEquip(Mobile from)
		{
			int strBonus = 0;
			int dexBonus = 0;
			int intBonus = 0;

			if ((strBonus != 0 || dexBonus != 0 || intBonus != 0))
			{
				Mobile m = from;

				string modName = Serial.ToString();

				if (strBonus != 0)
				{
					m.AddStatMod(new StatMod(StatType.Str, modName + "Str", strBonus, TimeSpan.Zero));
				}

				if (dexBonus != 0)
				{
					m.AddStatMod(new StatMod(StatType.Dex, modName + "Dex", dexBonus, TimeSpan.Zero));
				}

				if (intBonus != 0)
				{
					m.AddStatMod(new StatMod(StatType.Int, modName + "Int", intBonus, TimeSpan.Zero));
				}
			}

			from.NextCombatTime = Core.TickCount + (int)GetDelay(from).TotalMilliseconds;

			if (UseSkillMod && m_AccuracyLevel != WeaponAccuracyLevel.Regular)
			{
				if (m_SkillMod != null)
				{
					m_SkillMod.Remove();
				}

				m_SkillMod = new DefaultSkillMod(AccuracySkill, true, (int)m_AccuracyLevel * 5);
				from.AddSkillMod(m_SkillMod);
			}

			XmlAttach.CheckOnEquip(this, from);

			return true;
		}

		public override void OnAdded(object parent)
		{
			base.OnAdded(parent);

			if (parent is Mobile)
			{
				Mobile from = (Mobile)parent;

				from.CheckStatTimers();
				from.Delta(MobileDelta.WeaponDamage);
			}
		}

		public override void OnRemoved(object parent)
		{
			if (parent is Mobile)
			{
				Mobile m = (Mobile)parent;
				BaseWeapon weapon = m.Weapon as BaseWeapon;

				string modName = Serial.ToString();

				m.RemoveStatMod(modName + "Str");
				m.RemoveStatMod(modName + "Dex");
				m.RemoveStatMod(modName + "Int");

				if (weapon != null)
				{
					m.NextCombatTime = Core.TickCount + (int)weapon.GetDelay(m).TotalMilliseconds;
				}

				if (UseSkillMod && m_SkillMod != null)
				{
					m_SkillMod.Remove();
					m_SkillMod = null;
				}

				if (m_MageMod != null)
				{
					m_MageMod.Remove();
					m_MageMod = null;
				}

				m.CheckStatTimers();

				m.Delta(MobileDelta.WeaponDamage);

				XmlAttach.CheckOnRemoved(this, parent);

                if (FocusWeilder != null)
                    FocusWeilder = null;

			}
		}

		public virtual SkillName GetUsedSkill(Mobile m, bool checkSkillAttrs)
		{
			SkillName sk;

			if (checkSkillAttrs)
			{
				double swrd = m.Skills[SkillName.Swords].Value;
				double fenc = m.Skills[SkillName.Fencing].Value;
				double mcng = m.Skills[SkillName.Macing].Value;
				double val;

				sk = SkillName.Swords;
				val = swrd;

				if (fenc > val)
				{
					sk = SkillName.Fencing;
					val = fenc;
				}
				if (mcng > val)
				{
					sk = SkillName.Macing;
					val = mcng;
				}
			}
			else
			{
				sk = Skill;

				if (sk != SkillName.Wrestling && !m.Player && !m.Body.IsHuman &&
					m.Skills[SkillName.Wrestling].Value > m.Skills[sk].Value)
				{
					sk = SkillName.Wrestling;
				}
			}

			return sk;
		}

		public virtual double GetAttackSkillValue(Mobile attacker, Mobile defender)
		{
			return attacker.Skills[GetUsedSkill(attacker, true)].Value;
		}

		public virtual double GetDefendSkillValue(Mobile attacker, Mobile defender)
		{
			return defender.Skills[GetUsedSkill(defender, true)].Value;
		}

		public virtual bool CheckHit(Mobile attacker, Mobile defender)
		{
			BaseWeapon atkWeapon = attacker.Weapon as BaseWeapon;
			BaseWeapon defWeapon = defender.Weapon as BaseWeapon;

			Skill atkSkill = attacker.Skills[atkWeapon.Skill];
			Skill defSkill = defender.Skills[defWeapon.Skill];

			double atkValue = atkWeapon.GetAttackSkillValue(attacker, defender);
			double defValue = defWeapon.GetDefendSkillValue(attacker, defender);

			double ourValue, theirValue;

			int bonus = GetHitChanceBonus();

			if (atkValue <= -50.0)
			{
				atkValue = -49.9;
			}

			if (defValue <= -50.0)
			{
				defValue = -49.9;
			}

			ourValue = (atkValue + 50.0);
			theirValue = (defValue + 50.0);

			double chance = ourValue / (theirValue * 2.0);

			chance *= 1.0 + ((double)bonus / 100);

			if (Core.AOS && chance < 0.02)
			{
				chance = 0.02;
			}

			return attacker.CheckSkill(atkSkill.SkillName, chance);
		}

		public virtual TimeSpan GetDelay(Mobile m)
		{
			double speed = Speed;

			if (speed == 0)
			{
				return TimeSpan.FromHours(1.0);
			}

			double delayInSeconds;

			int v = (m.Stam + 100) * (int)speed;

			if (v <= 0)
			{
				v = 1;
			}

			delayInSeconds = 15000.0 / v;

			return TimeSpan.FromSeconds(delayInSeconds);
		}

		public virtual void OnBeforeSwing(Mobile attacker, Mobile defender)
		{
			WeaponAbility a = WeaponAbility.GetCurrentAbility(attacker);

			if (a != null && !a.OnBeforeSwing(attacker, defender))
			{
				WeaponAbility.ClearCurrentAbility(attacker);
			}

			SpecialMove move = SpecialMove.GetCurrentMove(attacker);

			if (move != null && !move.OnBeforeSwing(attacker, defender))
			{
				SpecialMove.ClearCurrentMove(attacker);
			}
		}

		public virtual TimeSpan OnSwing(Mobile attacker, Mobile defender)
		{
			return OnSwing(attacker, defender, 1.0);
		}

		public virtual TimeSpan OnSwing(Mobile attacker, Mobile defender, double damageBonus)
		{
			bool canSwing = true;

			if (Core.AOS)
			{
				canSwing = (!attacker.Paralyzed && !attacker.Frozen);

				if (canSwing)
				{
					Spell sp = attacker.Spell as Spell;

					canSwing = (sp == null || !sp.IsCasting || !sp.BlocksMovement);
				}

				if (canSwing)
				{
					PlayerMobile p = attacker as PlayerMobile;

					canSwing = (p == null || p.PeacedUntil <= DateTime.UtcNow);
				}
			}

			#region Dueling
			if (attacker is PlayerMobile)
			{
				PlayerMobile pm = (PlayerMobile)attacker;

				if (pm.DuelContext != null && !pm.DuelContext.CheckItemEquip(attacker, this))
				{
					canSwing = false;
				}
			}
			#endregion

			if (canSwing && attacker.HarmfulCheck(defender))
			{
				attacker.DisruptiveAction();

				if (attacker.NetState != null)
				{
					attacker.Send(new Swing(0, attacker, defender));
				}

				if (attacker is BaseCreature)
				{
					BaseCreature bc = (BaseCreature)attacker;
					WeaponAbility ab = bc.GetWeaponAbility();

					if (ab != null)
					{
						if (bc.WeaponAbilityChance > Utility.RandomDouble())
						{
							WeaponAbility.SetCurrentAbility(bc, ab);
						}
						else
						{
							WeaponAbility.ClearCurrentAbility(bc);
						}
					}
				}

				if (CheckHit(attacker, defender))
				{
					OnHit(attacker, defender, damageBonus);
				}
				else
				{
					OnMiss(attacker, defender);
				}
			}

			return GetDelay(attacker);
		}

		#region Sounds
		public virtual int GetHitAttackSound(Mobile attacker, Mobile defender)
		{
			int sound = attacker.GetAttackSound();

			if (sound == -1)
			{
				sound = HitSound;
			}

			return sound;
		}

		public virtual int GetHitDefendSound(Mobile attacker, Mobile defender)
		{
			return defender.GetHurtSound();
		}

		public virtual int GetMissAttackSound(Mobile attacker, Mobile defender)
		{
			if (attacker.GetAttackSound() == -1)
			{
				return MissSound;
			}
			else
			{
				return -1;
			}
		}

		public virtual int GetMissDefendSound(Mobile attacker, Mobile defender)
		{
			return -1;
		}
		#endregion

		public static bool CheckParry(Mobile defender)
		{
			if (defender == null)
			{
				return false;
			}

			BaseShield shield = defender.FindItemOnLayer(Layer.TwoHanded) as BaseShield;

			double parry = defender.Skills[SkillName.Parry].Value;
			double bushidoNonRacial = defender.Skills[SkillName.Bushido].NonRacialValue;
			double bushido = defender.Skills[SkillName.Bushido].Value;

			if (shield != null)
			{
				double chance = (parry - bushidoNonRacial) / 400.0;
					// As per OSI, no negitive effect from the Racial stuffs, ie, 120 parry and '0' bushido with humans

				if (chance < 0) // chance shouldn't go below 0
				{
					chance = 0;
				}

				// Parry/Bushido over 100 grants a 5% bonus.
				if (parry >= 100.0 || bushido >= 100.0)
				{
					chance += 0.05;
				}

				// Low dexterity lowers the chance.
				if (defender.Dex < 80)
				{
					chance = chance * (20 + defender.Dex) / 100;
				}

				return defender.CheckSkill(SkillName.Parry, chance);
			}
			else if (!(defender.Weapon is Fists) && !(defender.Weapon is BaseRanged))
			{
				BaseWeapon weapon = defender.Weapon as BaseWeapon;

				double divisor = (weapon.Layer == Layer.OneHanded) ? 48000.0 : 41140.0;

				double chance = (parry * bushido) / divisor;

				double aosChance = parry / 800.0;

				// Parry or Bushido over 100 grant a 5% bonus.
				if (parry >= 100.0)
				{
					chance += 0.05;
					aosChance += 0.05;
				}
				else if (bushido >= 100.0)
				{
					chance += 0.05;
				}

				// Low dexterity lowers the chance.
				if (defender.Dex < 80)
				{
					chance = chance * (20 + defender.Dex) / 100;
				}

				if (chance > aosChance)
				{
					return defender.CheckSkill(SkillName.Parry, chance);
				}
				else
				{
					return (aosChance > Utility.RandomDouble());
						// Only skillcheck if wielding a shield & there's no effect from Bushido
				}
			}

			return false;
		}

		public virtual int AbsorbDamageAOS(Mobile attacker, Mobile defender, int damage)
		{
			int originalDamage = damage;

			bool blocked = false;

			if (defender.Player || defender.Body.IsHuman)
			{
				blocked = CheckParry(defender);
                BaseWeapon weapon = defender.Weapon as BaseWeapon;

				if (blocked)
				{
					defender.FixedEffect(0x37B9, 10, 16);
					damage = 0;

					BaseShield shield = defender.FindItemOnLayer(Layer.TwoHanded) as BaseShield;

					if (shield != null)
					{
						shield.OnHit(this, damage);

                        XmlAttach.OnArmorHit(attacker, defender, shield, this, originalDamage);
                    }
                }
			}

			if (!blocked)
			{
				double positionChance = Utility.RandomDouble();

                Layer randomLayer = _DamageLayers[Utility.Random(_DamageLayers.Length)];
                Item armorItem = defender.FindItemOnLayer(randomLayer);

				IWearableDurability armor = armorItem as IWearableDurability;

				if (armor != null)
				{
					armor.OnHit(this, damage); // call OnHit to lose durability
					damage -= XmlAttach.OnArmorHit(attacker, defender, armorItem, this, originalDamage);
				}
			}

			return damage;
		}

        private Layer[] _DamageLayers =
        {
            Layer.Talisman,
            Layer.InnerLegs,
            Layer.InnerTorso,
            Layer.MiddleTorso,
            Layer.Waist,
            Layer.Cloak,
            Layer.OuterTorso,
            Layer.Ring,
            Layer.Bracelet,
            Layer.Neck,
            Layer.Neck,
            Layer.Gloves,
            Layer.Gloves,
            Layer.Arms,
            Layer.Arms,
            Layer.Helm,
            Layer.Helm,
            Layer.Pants,
            Layer.Pants,
            Layer.Pants,
            Layer.Shirt,
            Layer.Shirt,
            Layer.Shirt
        };

		public virtual int AbsorbDamage(Mobile attacker, Mobile defender, int damage)
		{
			if (Core.AOS)
			{
				return AbsorbDamageAOS(attacker, defender, damage);
			}

			BaseShield shield = defender.FindItemOnLayer(Layer.TwoHanded) as BaseShield;
			if (shield != null)
			{
				damage = shield.OnHit(this, damage);
			}

			double chance = Utility.RandomDouble();

			Item armorItem;

			if (chance < 0.07)
			{
				armorItem = defender.NeckArmor;
			}
			else if (chance < 0.14)
			{
				armorItem = defender.HandArmor;
			}
			else if (chance < 0.28)
			{
				armorItem = defender.ArmsArmor;
			}
			else if (chance < 0.43)
			{
				armorItem = defender.HeadArmor;
			}
			else if (chance < 0.65)
			{
				armorItem = defender.LegsArmor;
			}
			else
			{
				armorItem = defender.ChestArmor;
			}

			IWearableDurability armor = armorItem as IWearableDurability;

			if (armor != null)
			{
				damage = armor.OnHit(this, damage);
			}

			int virtualArmor = defender.VirtualArmor + defender.VirtualArmorMod;

			damage -= XmlAttach.OnArmorHit(attacker, defender, armorItem, this, damage);
			damage -= XmlAttach.OnArmorHit(attacker, defender, shield, this, damage);

			if (virtualArmor > 0)
			{
				double scalar;

				if (chance < 0.14)
				{
					scalar = 0.07;
				}
				else if (chance < 0.28)
				{
					scalar = 0.14;
				}
				else if (chance < 0.43)
				{
					scalar = 0.15;
				}
				else if (chance < 0.65)
				{
					scalar = 0.22;
				}
				else
				{
					scalar = 0.35;
				}

				int from = (int)(virtualArmor * scalar) / 2;
				int to = (int)(virtualArmor * scalar);

				damage -= Utility.Random(from, (to - from) + 1);
			}

			return damage;
		}

		public virtual int GetPackInstinctBonus(Mobile attacker, Mobile defender)
		{
			if (attacker.Player || defender.Player)
			{
				return 0;
			}

			BaseCreature bc = attacker as BaseCreature;

			if (bc == null || bc.PackInstinct == PackInstinct.None || (!bc.Controlled && !bc.Summoned))
			{
				return 0;
			}

			Mobile master = bc.ControlMaster;

			if (master == null)
			{
				master = bc.SummonMaster;
			}

			if (master == null)
			{
				return 0;
			}

			int inPack = 1;

			foreach (Mobile m in defender.GetMobilesInRange(1))
			{
				if (m != attacker && m is BaseCreature)
				{
					BaseCreature tc = (BaseCreature)m;

					if ((tc.PackInstinct & bc.PackInstinct) == 0 || (!tc.Controlled && !tc.Summoned))
					{
						continue;
					}

					Mobile theirMaster = tc.ControlMaster;

					if (theirMaster == null)
					{
						theirMaster = tc.SummonMaster;
					}

					if (master == theirMaster && tc.Combatant == defender)
					{
						++inPack;
					}
				}
			}

			if (inPack >= 5)
			{
				return 100;
			}
			else if (inPack >= 4)
			{
				return 75;
			}
			else if (inPack >= 3)
			{
				return 50;
			}
			else if (inPack >= 2)
			{
				return 25;
			}

			return 0;
		}

		private static bool m_InDoubleStrike;

		public static bool InDoubleStrike { get { return m_InDoubleStrike; } set { m_InDoubleStrike = value; } }

		public void OnHit(Mobile attacker, Mobile defender)
		{
			OnHit(attacker, defender, 1.0);
		}

        /// <summary>
        /// 020416 Crome696 : Adding concecrate Weapon Bonus
        /// </summary>
        /// <param name="attacker"></param>
        /// <param name="defender"></param>
        /// <param name="damageBonus"></param>
		public virtual void OnHit(Mobile attacker, Mobile defender, double damageBonus)
		{
			PlaySwingAnimation(attacker);
			PlayHurtAnimation(defender);

			attacker.PlaySound(GetHitAttackSound(attacker, defender));
			defender.PlaySound(GetHitDefendSound(attacker, defender));

			int damage = ComputeDamage(attacker, defender);

			if (attacker is BaseCreature)
			{
				((BaseCreature)attacker).AlterMeleeDamageTo(defender, ref damage);
			}

			if (defender is BaseCreature)
			{
				((BaseCreature)defender).AlterMeleeDamageFrom(attacker, ref damage);
			}

			damage = AbsorbDamage(attacker, defender, damage);

			if (damage < 1)
			{
				damage = 1;
			}

			AddBlood(attacker, defender, damage);

			int damageGiven = damage;

			damageGiven = AOS.Damage(
				defender,
				attacker,
				damage,
				ignoreArmor,
				phys,
				fire,
				cold,
				pois,
				nrgy,
				chaos,
				direct,
				false,
				this is BaseRanged,
				false);

			double propertyBonus = (move == null) ? 1.0 : move.GetPropertyBonus(attacker);

			if (m_MaxHits > 0 &&
				((MaxRange <= 1 && (defender is Slime || defender is ToxicElemental || defender is CorrosiveSlime)) ||
				 Utility.Random(250) == 0)) // Stratics says 50% chance, seems more like 4%..
			{
				if (MaxRange <= 1 && (defender is Slime || defender is ToxicElemental || defender is CorrosiveSlime))
				{
					attacker.LocalOverheadMessage(MessageType.Regular, 0x3B2, 500263); // *Acid blood scars your weapon!*
				}

				else
				{
					if (m_Hits > 0)
					{
                        HitPoints -= 1;
					}
					else if (m_MaxHits > 1)
					{
                        MaxHitPoints -= 1;

						if (Parent is Mobile)
						{
							((Mobile)Parent).LocalOverheadMessage(MessageType.Regular, 0x3B2, 1061121);
								// Your equipment is severely damaged.
						}
					}
					else
					{
						Delete();
					}
				}
			}

			if (attacker is BaseCreature)
			{
				((BaseCreature)attacker).OnGaveMeleeAttack(defender);
			}

			if (defender is BaseCreature)
			{
				((BaseCreature)defender).OnGotMeleeAttack(attacker);
			}

			if (a != null)
			{
				a.OnHit(attacker, defender, damage);
			}

			if (move != null)
			{
				move.OnHit(attacker, defender, damage);
			}

			if (defender is IHonorTarget && ((IHonorTarget)defender).ReceivedHonorContext != null)
			{
				((IHonorTarget)defender).ReceivedHonorContext.OnTargetHit(attacker);
			}

			XmlAttach.OnWeaponHit(this, attacker, defender, damageGiven);
		}

		public virtual double GetAosDamage(Mobile attacker, int bonus, int dice, int sides)
		{
			int damage = Utility.Dice(dice, sides, bonus) * 100;
			int damageBonus = 0;

			// Inscription bonus
			int inscribeSkill = attacker.Skills[SkillName.Inscribe].Fixed;



			if (inscribeSkill >= 1000)
			{
				damageBonus += 10;
			}

			if (attacker.Player)
			{
				// Int bonus
				damageBonus += (attacker.Int / 10);
			}

			damage = MathHelper.Scale(damage, 100 + damageBonus);

			return damage / 100;
		}

		#region Do<AoSEffect>
		public virtual void DoMagicArrow(Mobile attacker, Mobile defender)
		{
			if (!attacker.CanBeHarmful(defender, false))
			{
				return;
			}

			attacker.DoHarmful(defender);

			double damage = GetAosDamage(attacker, 10, 1, 4);

			attacker.MovingParticles(defender, 0x36E4, 5, 0, false, true, 3006, 4006, 0);
			attacker.PlaySound(0x1E5);

			SpellHelper.Damage(TimeSpan.FromSeconds(1.0), defender, attacker, damage, 0, 100, 0, 0, 0);
		}

		public virtual void DoHarm(Mobile attacker, Mobile defender)
		{
			if (!attacker.CanBeHarmful(defender, false))
			{
				return;
			}

			attacker.DoHarmful(defender);

			double damage = GetAosDamage(attacker, 17, 1, 5);

			if (!defender.InRange(attacker, 2))
			{
				damage *= 0.25; // 1/4 damage at > 2 tile range
			}
			else if (!defender.InRange(attacker, 1))
			{
				damage *= 0.50; // 1/2 damage at 2 tile range
			}

			defender.FixedParticles(0x374A, 10, 30, 5013, 1153, 2, EffectLayer.Waist);
			defender.PlaySound(0x0FC);

			SpellHelper.Damage(TimeSpan.Zero, defender, attacker, damage, 0, 0, 100, 0, 0);
		}

		public virtual void DoFireball(Mobile attacker, Mobile defender)
		{
			if (!attacker.CanBeHarmful(defender, false))
			{
				return;
			}

			attacker.DoHarmful(defender);

			double damage = GetAosDamage(attacker, 19, 1, 5);

			attacker.MovingParticles(defender, 0x36D4, 7, 0, false, true, 9502, 4019, 0x160);
			attacker.PlaySound(0x15E);

			SpellHelper.Damage(TimeSpan.FromSeconds(1.0), defender, attacker, damage, 0, 100, 0, 0, 0);
		}

		public virtual void DoLightning(Mobile attacker, Mobile defender)
		{
			if (!attacker.CanBeHarmful(defender, false))
			{
				return;
			}

			attacker.DoHarmful(defender);

			double damage = GetAosDamage(attacker, 23, 1, 4);

			defender.BoltEffect(0);

			SpellHelper.Damage(TimeSpan.Zero, defender, attacker, damage, 0, 0, 0, 0, 100);
		}

		public virtual void DoDispel(Mobile attacker, Mobile defender)
		{
			bool dispellable = false;

			if (defender is BaseCreature)
			{
				dispellable = ((BaseCreature)defender).Summoned && !((BaseCreature)defender).IsAnimatedDead;
			}

			if (!dispellable)
			{
				return;
			}

			if (!attacker.CanBeHarmful(defender, false))
			{
				return;
			}

			attacker.DoHarmful(defender);

			MagerySpell sp = new DispelSpell(attacker, null);

			if (sp.CheckResisted(defender))
			{
				defender.FixedEffect(0x3779, 10, 20);
			}
			else
			{
				Effects.SendLocationParticles(
					EffectItem.Create(defender.Location, defender.Map, EffectItem.DefaultDuration), 0x3728, 8, 20, 5042);
				Effects.PlaySound(defender, defender.Map, 0x201);

				defender.Delete();
			}
		}

		#region Stygian Abyss
		public virtual void DoCurse(Mobile attacker, Mobile defender)
		{
			attacker.SendLocalizedMessage(1113717); // You have hit your target with a curse effect.
			defender.SendLocalizedMessage(1113718); // You have been hit with a curse effect.
			defender.FixedParticles(0x374A, 10, 15, 5028, EffectLayer.Waist);
			defender.PlaySound(0x1EA);
			defender.AddStatMod(
				new StatMod(StatType.Str, String.Format("[Magic] {0} Curse", StatType.Str), -10, TimeSpan.FromSeconds(30)));
			defender.AddStatMod(
				new StatMod(StatType.Dex, String.Format("[Magic] {0} Curse", StatType.Dex), -10, TimeSpan.FromSeconds(30)));
			defender.AddStatMod(
				new StatMod(StatType.Int, String.Format("[Magic] {0} Curse", StatType.Int), -10, TimeSpan.FromSeconds(30)));

			int percentage = -10; //(int)(SpellHelper.GetOffsetScalar(Caster, m, true) * 100);
			string args = String.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}", percentage, percentage, percentage, 10, 10, 10, 10);

			BuffInfo.AddBuff(defender, new BuffInfo(BuffIcon.Curse, 1075835, 1075836, TimeSpan.FromSeconds(30), defender, args));
		}

		public virtual void DoFatigue(Mobile attacker, Mobile defender, int damagegiven)
		{
			// Message?
			// Effects?
			defender.Stam -= (damagegiven * (100 - m_AosWeaponAttributes.HitFatigue)) / 100;
		}

		public virtual void DoManaDrain(Mobile attacker, Mobile defender, int damagegiven)
		{
			// Message?
			defender.FixedParticles(0x3789, 10, 25, 5032, EffectLayer.Head);
			defender.PlaySound(0x1F8);
			defender.Mana -= (damagegiven * (100 - m_AosWeaponAttributes.HitManaDrain)) / 100;
		}
		#endregion

		public virtual void DoLowerAttack(Mobile from, Mobile defender)
		{
			if (HitLower.ApplyAttack(defender))
			{
				defender.PlaySound(0x28E);
				Effects.SendTargetEffect(defender, 0x37BE, 1, 4, 0xA, 3);
			}
		}

		public virtual void DoLowerDefense(Mobile from, Mobile defender)
		{
			if (HitLower.ApplyDefense(defender))
			{
				defender.PlaySound(0x28E);
				Effects.SendTargetEffect(defender, 0x37BE, 1, 4, 0x23, 3);
			}
		}

		public virtual void DoAreaAttack(
			Mobile from, Mobile defender, int sound, int hue, int phys, int fire, int cold, int pois, int nrgy)
		{
			Map map = from.Map;

			if (map == null)
			{
				return;
			}

			var list = new List<Mobile>();

			foreach (Mobile m in from.GetMobilesInRange(10))
			{
				if (from != m && defender != m && SpellHelper.ValidIndirectTarget(from, m) && from.CanBeHarmful(m, false) &&
					(!Core.ML || from.InLOS(m)))
				{
					list.Add(m);
				}
			}

			if (list.Count == 0)
			{
				return;
			}

			Effects.PlaySound(from.Location, map, sound);

			// TODO: What is the damage calculation?

			for (int i = 0; i < list.Count; ++i)
			{
				Mobile m = list[i];

				double scalar = (11 - from.GetDistanceToSqrt(m)) / 10;

				if (scalar > 1.0)
				{
					scalar = 1.0;
				}
				else if (scalar < 0.0)
				{
					continue;
				}

				from.DoHarmful(m, true);
				m.FixedEffect(0x3779, 1, 15, hue, 0);
				AOS.Damage(m, from, (int)(GetBaseDamage(from) * scalar), phys, fire, cold, pois, nrgy);
			}
		}
		#endregion

		public virtual CheckSlayerResult CheckSlayers(Mobile attacker, Mobile defender)
		{
			BaseWeapon atkWeapon = attacker.Weapon as BaseWeapon;
			SlayerEntry atkSlayer = SlayerGroup.GetEntryByName(atkWeapon.Slayer);
			SlayerEntry atkSlayer2 = SlayerGroup.GetEntryByName(atkWeapon.Slayer2);

            List<SlayerName> super = new List<SlayerName>() {SlayerName.Repond, SlayerName.Silver, SlayerName.Fey, SlayerName.ElementalBan, SlayerName.Exorcism, SlayerName.ArachnidDoom, SlayerName.ReptilianDeath};

		    if ((atkSlayer != null && atkSlayer.Slays(defender) && super.Contains(atkSlayer.Name)) || (atkSlayer2 != null && atkSlayer2.Slays(defender) && super.Contains(atkSlayer2.Name)))
		    {
		        return CheckSlayerResult.SuperSlayer;
		    }

            if (atkSlayer != null && atkSlayer.Slays(defender) || atkSlayer2 != null && atkSlayer2.Slays(defender))
			{
				return CheckSlayerResult.Slayer;
			}

			return CheckSlayerResult.None;
		}

		public virtual void AddBlood(Mobile attacker, Mobile defender, int damage)
		{
			if (damage > 0)
			{
				new Blood().MoveToWorld(defender.Location, defender.Map);

				int extraBlood = (Core.SE ? Utility.RandomMinMax(3, 4) : Utility.RandomMinMax(0, 1));

				for (int i = 0; i < extraBlood; i++)
				{
					new Blood().MoveToWorld(
						new Point3D(defender.X + Utility.RandomMinMax(-1, 1), defender.Y + Utility.RandomMinMax(-1, 1), defender.Z),
						defender.Map);
				}
			}
		}

		private int ApplyCraftAttributeElementDamage(int attrDamage, ref int element, int totalRemaining)
		{
			if (totalRemaining <= 0)
			{
				return 0;
			}

			if (attrDamage <= 0)
			{
				return totalRemaining;
			}

			int appliedDamage = attrDamage;

			if ((appliedDamage + element) > 100)
			{
				appliedDamage = 100 - element;
			}

			if (appliedDamage > totalRemaining)
			{
				appliedDamage = totalRemaining;
			}

			element += appliedDamage;

			return totalRemaining - appliedDamage;
		}

		public virtual void OnMiss(Mobile attacker, Mobile defender)
		{
			PlaySwingAnimation(attacker);
			attacker.PlaySound(GetMissAttackSound(attacker, defender));
			defender.PlaySound(GetMissDefendSound(attacker, defender));

			WeaponAbility ability = WeaponAbility.GetCurrentAbility(attacker);

			if (ability != null)
			{
				ability.OnMiss(attacker, defender);
			}

			SpecialMove move = SpecialMove.GetCurrentMove(attacker);

			if (move != null)
			{
				move.OnMiss(attacker, defender);
			}

			if (defender is IHonorTarget && ((IHonorTarget)defender).ReceivedHonorContext != null)
			{
				((IHonorTarget)defender).ReceivedHonorContext.OnTargetMissed(attacker);
			}
		}

		public virtual void GetBaseDamageRange(Mobile attacker, out int min, out int max)
		{
			if (attacker is BaseCreature)
			{
				BaseCreature c = (BaseCreature)attacker;

				if (c.DamageMin >= 0)
				{
					min = c.DamageMin;
					max = c.DamageMax;
					return;
				}

				if (this is Fists && !attacker.Body.IsHuman)
				{
					min = attacker.Str / 28;
					max = attacker.Str / 28;
					return;
				}
			}

			min = MinDamage;
			max = MaxDamage;
		}

		public virtual double GetBaseDamage(Mobile attacker)
		{
			int min, max;

			GetBaseDamageRange(attacker, out min, out max);

			int damage = Utility.RandomMinMax(min, max);

			if (Core.AOS)
			{
				return damage;
			}

			/* Apply damage level offset
             * : Regular : 0
             * : Ruin    : 1
             * : Might   : 3
             * : Force   : 5
             * : Power   : 7
             * : Vanq    : 9
             */
			if (m_DamageLevel != WeaponDamageLevel.Regular)
			{
				damage += (2 * (int)m_DamageLevel) - 1;
			}

			return damage;
		}

		public virtual double GetBonus(double value, double scalar, double threshold, double offset)
		{
			double bonus = value * scalar;

			if (value >= threshold)
			{
				bonus += offset;
			}

			return bonus / 100;
		}

		public virtual int GetHitChanceBonus()
		{
			if (!Core.AOS)
			{
				return 0;
			}

			int bonus = 0;

			switch (m_AccuracyLevel)
			{
				case WeaponAccuracyLevel.Accurate:
					bonus += 02;
					break;
				case WeaponAccuracyLevel.Surpassingly:
					bonus += 04;
					break;
				case WeaponAccuracyLevel.Eminently:
					bonus += 06;
					break;
				case WeaponAccuracyLevel.Exceedingly:
					bonus += 08;
					break;
				case WeaponAccuracyLevel.Supremely:
					bonus += 10;
					break;
			}

			return bonus;
		}

		public virtual int GetDamageBonus()
		{
			int bonus = VirtualDamageBonus;

			if (!Core.AOS)
			{
				switch (m_Quality)
				{
					case WeaponQuality.Low:
						bonus -= 20;
						break;
					case WeaponQuality.Exceptional:
						bonus += 20;
						break;
				}

				switch (m_DamageLevel)
				{
					case WeaponDamageLevel.Ruin:
						bonus += 15;
						break;
					case WeaponDamageLevel.Might:
						bonus += 20;
						break;
					case WeaponDamageLevel.Force:
						bonus += 25;
						break;
					case WeaponDamageLevel.Power:
						bonus += 30;
						break;
					case WeaponDamageLevel.Vanq:
						bonus += 35;
						break;
				}
			}

			return bonus;
		}

		public virtual void GetStatusDamage(Mobile from, out int min, out int max)
		{
			int baseMin, baseMax;

			GetBaseDamageRange(from, out baseMin, out baseMax);

			if (Core.AOS)
			{
				min = Math.Max((int)ScaleDamageAOS(from, baseMin, false), 1);
				max = Math.Max((int)ScaleDamageAOS(from, baseMax, false), 1);
			}
			else
			{
				min = Math.Max((int)ScaleDamageOld(from, baseMin, false), 1);
				max = Math.Max((int)ScaleDamageOld(from, baseMax, false), 1);
			}
		}

		public virtual double ScaleDamageAOS(Mobile attacker, double damage, bool checkSkills)
		{
			if (checkSkills)
			{
				attacker.CheckSkill(SkillName.Tactics, 0.0, attacker.Skills[SkillName.Tactics].Cap);
					// Passively check tactics for gain
				attacker.CheckSkill(SkillName.Anatomy, 0.0, attacker.Skills[SkillName.Anatomy].Cap);
					// Passively check Anatomy for gain

				if (Type == WeaponType.Axe)
				{
					attacker.CheckSkill(SkillName.Lumberjacking, 0.0, 100.0); // Passively check Lumberjacking for gain
				}
			}

			#region Physical bonuses
			/*
            * These are the bonuses given by the physical characteristics of the mobile.
            * No caps apply.
            */
			double strengthBonus = GetBonus(attacker.Str, 0.300, 100.0, 5.00);
			double anatomyBonus = GetBonus(attacker.Skills[SkillName.Anatomy].Value, 0.500, 100.0, 5.00);
			double tacticsBonus = GetBonus(attacker.Skills[SkillName.Tactics].Value, 0.625, 100.0, 6.25);
			double lumberBonus = GetBonus(attacker.Skills[SkillName.Lumberjacking].Value, 0.200, 100.0, 10.00);

			if (Type != WeaponType.Axe)
			{
				lumberBonus = 0.0;
			}
			#endregion

			#region Modifiers
			/*
            * The following are damage modifiers whose effect shows on the status bar.
            * Capped at 100% total.
            */
			int damageBonus = 0;

			int discordanceEffect = 0;

			// Discordance gives a -2%/-48% malus to damage.
			if (Discordance.GetEffect(attacker, ref discordanceEffect))
			{
				damageBonus -= discordanceEffect * 2;
			}

			if (damageBonus > 100)
			{
				damageBonus = 100;
			}
			#endregion

			double totalBonus = strengthBonus + anatomyBonus + tacticsBonus + lumberBonus +
								((GetDamageBonus() + damageBonus) / 100.0);

			return damage + (int)(damage * totalBonus);
		}

		public virtual int VirtualDamageBonus { get { return 0; } }

		public virtual int ComputeDamageAOS(Mobile attacker, Mobile defender)
		{
			return (int)ScaleDamageAOS(attacker, GetBaseDamage(attacker), true);
		}

		public virtual double ScaleDamageOld(Mobile attacker, double damage, bool checkSkills)
		{
			if (checkSkills)
			{
				attacker.CheckSkill(SkillName.Tactics, 0.0, attacker.Skills[SkillName.Tactics].Cap);
					// Passively check tactics for gain
				attacker.CheckSkill(SkillName.Anatomy, 0.0, attacker.Skills[SkillName.Anatomy].Cap);
					// Passively check Anatomy for gain

				if (Type == WeaponType.Axe)
				{
					attacker.CheckSkill(SkillName.Lumberjacking, 0.0, 100.0); // Passively check Lumberjacking for gain
				}
			}

			/* Compute tactics modifier
            * :   0.0 = 50% loss
            * :  50.0 = unchanged
            * : 100.0 = 50% bonus
            */
			damage += (damage * ((attacker.Skills[SkillName.Tactics].Value - 50.0) / 100.0));

			/* Compute strength modifier
            * : 1% bonus for every 5 strength
            */
			double modifiers = (attacker.Str / 5.0) / 100.0;

			/* Compute anatomy modifier
            * : 1% bonus for every 5 points of anatomy
            * : +10% bonus at Grandmaster or higher
            */
			double anatomyValue = attacker.Skills[SkillName.Anatomy].Value;
			modifiers += ((anatomyValue / 5.0) / 100.0);

			if (anatomyValue >= 100.0)
			{
				modifiers += 0.1;
			}

			/* Compute lumberjacking bonus
            * : 1% bonus for every 5 points of lumberjacking
            * : +10% bonus at Grandmaster or higher
            */

			if (Type == WeaponType.Axe)
			{
				double lumberValue = attacker.Skills[SkillName.Lumberjacking].Value;
			    lumberValue = (lumberValue/5.0)/100.0;
			    if (lumberValue > 0.2)
			        lumberValue = 0.2;
			    
				modifiers += lumberValue;

				if (lumberValue >= 100.0)
				{
					modifiers += 0.1;
				}
			}

			// New quality bonus:
			if (m_Quality != WeaponQuality.Regular)
			{
				modifiers += (((int)m_Quality - 1) * 0.2);
			}

			// Virtual damage bonus:
			if (VirtualDamageBonus != 0)
			{
				modifiers += (VirtualDamageBonus / 100.0);
			}

			// Apply bonuses
			damage += (damage * modifiers);

			return ScaleDamageByDurability((int)damage);
		}

		public virtual int ScaleDamageByDurability(int damage)
		{
			int scale = 100;

			if (m_MaxHits > 0 && m_Hits < m_MaxHits)
			{
				scale = 50 + ((50 * m_Hits) / m_MaxHits);
			}

			return MathHelper.Scale(damage, scale);
		}

		public virtual int ComputeDamage(Mobile attacker, Mobile defender)
		{
			if (Core.AOS)
			{
				return ComputeDamageAOS(attacker, defender);
			}

			int damage = (int)ScaleDamageOld(attacker, GetBaseDamage(attacker), true);

			// pre-AOS, halve damage if the defender is a player or the attacker is not a player
			if (defender is PlayerMobile || !(attacker is PlayerMobile))
			{
				damage = (int)(damage / 2.0);
			}

			return damage;
		}

		public virtual void PlayHurtAnimation(Mobile from)
		{
			int action;
			int frames;

			switch (from.Body.Type)
			{
				case BodyType.Sea:
				case BodyType.Animal:
					{
						action = 7;
						frames = 5;
						break;
					}
				case BodyType.Monster:
					{
						action = 10;
						frames = 4;
						break;
					}
				case BodyType.Human:
					{
						action = 20;
						frames = 5;
						break;
					}
				default:
					return;
			}

			if (from.Mounted)
			{
				return;
			}

			from.Animate(action, frames, 1, true, false, 0);
		}

		public virtual void PlaySwingAnimation(Mobile from)
		{
			int action;

			switch (from.Body.Type)
			{
				case BodyType.Sea:
				case BodyType.Animal:
					{
						action = Utility.Random(5, 2);
						break;
					}
				case BodyType.Monster:
					{
						switch (Animation)
						{
							default:
							case WeaponAnimation.Wrestle:
							case WeaponAnimation.Bash1H:
							case WeaponAnimation.Pierce1H:
							case WeaponAnimation.Slash1H:
							case WeaponAnimation.Bash2H:
							case WeaponAnimation.Pierce2H:
							case WeaponAnimation.Slash2H:
								action = Utility.Random(4, 3);
								break;
							case WeaponAnimation.ShootBow:
								return; // 7
							case WeaponAnimation.ShootXBow:
								return; // 8
						}

						break;
					}
				case BodyType.Human:
					{
						if (!from.Mounted)
						{
							action = (int)Animation;
						}
						else
						{
							switch (Animation)
							{
								default:
								case WeaponAnimation.Wrestle:
								case WeaponAnimation.Bash1H:
								case WeaponAnimation.Pierce1H:
								case WeaponAnimation.Slash1H:
									action = 26;
									break;
								case WeaponAnimation.Bash2H:
								case WeaponAnimation.Pierce2H:
								case WeaponAnimation.Slash2H:
									action = 29;
									break;
								case WeaponAnimation.ShootBow:
									action = 27;
									break;
								case WeaponAnimation.ShootXBow:
									action = 28;
									break;
							}
						}

						break;
					}
				default:
					return;
			}

			from.Animate(action, 7, 1, true, false, 0);
		}

		#region Serialization/Deserialization
		private static void SetSaveFlag(ref SaveFlag flags, SaveFlag toSet, bool setIf)
		{
			if (setIf)
			{
				flags |= toSet;
			}
		}

		private static bool GetSaveFlag(SaveFlag flags, SaveFlag toGet)
		{
			return ((flags & toGet) != 0);
		}

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write(14); // version

            // Version 10
			writer.Write(m_BlessedBy); // Bless Deed

			#region Veteran Rewards
			writer.Write(m_EngravedText);
			#endregion

			// Version 9
			SaveFlag flags = SaveFlag.None;

			SetSaveFlag(ref flags, SaveFlag.DamageLevel, m_DamageLevel != WeaponDamageLevel.Regular);
			SetSaveFlag(ref flags, SaveFlag.AccuracyLevel, m_AccuracyLevel != WeaponAccuracyLevel.Regular);
			SetSaveFlag(ref flags, SaveFlag.DurabilityLevel, m_DurabilityLevel != WeaponDurabilityLevel.Regular);
			SetSaveFlag(ref flags, SaveFlag.Quality, m_Quality != WeaponQuality.Regular);
			SetSaveFlag(ref flags, SaveFlag.Hits, m_Hits != 0);
			SetSaveFlag(ref flags, SaveFlag.MaxHits, m_MaxHits != 0);
			SetSaveFlag(ref flags, SaveFlag.Slayer, m_Slayer != SlayerName.None);
			SetSaveFlag(ref flags, SaveFlag.Poison, m_Poison != null);
			SetSaveFlag(ref flags, SaveFlag.PoisonCharges, m_PoisonCharges != 0);
			SetSaveFlag(ref flags, SaveFlag.Crafter, m_Crafter != null);
			SetSaveFlag(ref flags, SaveFlag.Identified, m_Identified);
			SetSaveFlag(ref flags, SaveFlag.StrReq, m_StrReq != -1);
			SetSaveFlag(ref flags, SaveFlag.DexReq, m_DexReq != -1);
			SetSaveFlag(ref flags, SaveFlag.IntReq, m_IntReq != -1);
			SetSaveFlag(ref flags, SaveFlag.MinDamage, m_MinDamage != -1);
			SetSaveFlag(ref flags, SaveFlag.MaxDamage, m_MaxDamage != -1);
			SetSaveFlag(ref flags, SaveFlag.HitSound, m_HitSound != -1);
			SetSaveFlag(ref flags, SaveFlag.MissSound, m_MissSound != -1);
			SetSaveFlag(ref flags, SaveFlag.Speed, m_Speed != -1);
			SetSaveFlag(ref flags, SaveFlag.MaxRange, m_MaxRange != -1);
			SetSaveFlag(ref flags, SaveFlag.Skill, m_Skill != (SkillName)(-1));
			SetSaveFlag(ref flags, SaveFlag.Type, m_Type != (WeaponType)(-1));
			SetSaveFlag(ref flags, SaveFlag.Animation, m_Animation != (WeaponAnimation)(-1));
			SetSaveFlag(ref flags, SaveFlag.Resource, m_Resource != CraftResource.Iron);
			SetSaveFlag(ref flags, SaveFlag.PlayerConstructed, m_PlayerConstructed);
			SetSaveFlag(ref flags, SaveFlag.Slayer2, m_Slayer2 != SlayerName.None);
			SetSaveFlag(ref flags, SaveFlag.EngravedText, !String.IsNullOrEmpty(m_EngravedText));

			writer.Write((long)flags);

			if (GetSaveFlag(flags, SaveFlag.DamageLevel))
			{
				writer.Write((int)m_DamageLevel);
			}

			if (GetSaveFlag(flags, SaveFlag.AccuracyLevel))
			{
				writer.Write((int)m_AccuracyLevel);
			}

			if (GetSaveFlag(flags, SaveFlag.DurabilityLevel))
			{
				writer.Write((int)m_DurabilityLevel);
			}

			if (GetSaveFlag(flags, SaveFlag.Quality))
			{
				writer.Write((int)m_Quality);
			}

			if (GetSaveFlag(flags, SaveFlag.Hits))
			{
				writer.Write(m_Hits);
			}

			if (GetSaveFlag(flags, SaveFlag.MaxHits))
			{
				writer.Write(m_MaxHits);
			}

			if (GetSaveFlag(flags, SaveFlag.Slayer))
			{
				writer.Write((int)m_Slayer);
			}

			if (GetSaveFlag(flags, SaveFlag.Poison))
			{
				Poison.Serialize(m_Poison, writer);
			}

			if (GetSaveFlag(flags, SaveFlag.PoisonCharges))
			{
				writer.Write(m_PoisonCharges);
			}

			if (GetSaveFlag(flags, SaveFlag.Crafter))
			{
				writer.Write(m_Crafter);
			}

			if (GetSaveFlag(flags, SaveFlag.StrReq))
			{
				writer.Write(m_StrReq);
			}

			if (GetSaveFlag(flags, SaveFlag.DexReq))
			{
				writer.Write(m_DexReq);
			}

			if (GetSaveFlag(flags, SaveFlag.IntReq))
			{
				writer.Write(m_IntReq);
			}

			if (GetSaveFlag(flags, SaveFlag.MinDamage))
			{
				writer.Write(m_MinDamage);
			}

			if (GetSaveFlag(flags, SaveFlag.MaxDamage))
			{
				writer.Write(m_MaxDamage);
			}

			if (GetSaveFlag(flags, SaveFlag.HitSound))
			{
				writer.Write(m_HitSound);
			}

			if (GetSaveFlag(flags, SaveFlag.MissSound))
			{
				writer.Write(m_MissSound);
			}

			if (GetSaveFlag(flags, SaveFlag.Speed))
			{
				writer.Write(m_Speed);
			}

			if (GetSaveFlag(flags, SaveFlag.MaxRange))
			{
				writer.Write(m_MaxRange);
			}

			if (GetSaveFlag(flags, SaveFlag.Skill))
			{
				writer.Write((int)m_Skill);
			}

			if (GetSaveFlag(flags, SaveFlag.Type))
			{
				writer.Write((int)m_Type);
			}

			if (GetSaveFlag(flags, SaveFlag.Animation))
			{
				writer.Write((int)m_Animation);
			}

			if (GetSaveFlag(flags, SaveFlag.Resource))
			{
				writer.Write((int)m_Resource);
			}

			if (GetSaveFlag(flags, SaveFlag.Slayer2))
			{
				writer.Write((int)m_Slayer2);
			}

			if (GetSaveFlag(flags, SaveFlag.EngravedText))
			{
				writer.Write(m_EngravedText);
			}

		}

		[Flags]
		private enum SaveFlag : long
		{
			None = 0x00000000,
			DamageLevel = 0x00000001,
			AccuracyLevel = 0x00000002,
			DurabilityLevel = 0x00000004,
			Quality = 0x00000008,
			Hits = 0x00000010,
			MaxHits = 0x00000020,
			Slayer = 0x00000040,
			Poison = 0x00000080,
			PoisonCharges = 0x00000100,
			Crafter = 0x00000200,
			Identified = 0x00000400,
			StrReq = 0x00000800,
			DexReq = 0x00001000,
			IntReq = 0x00002000,
			MinDamage = 0x00004000,
			MaxDamage = 0x00008000,
			HitSound = 0x00010000,
			MissSound = 0x00020000,
			Speed = 0x00040000,
			MaxRange = 0x00080000,
			Skill = 0x00100000,
			Type = 0x00200000,
			Animation = 0x00400000,
			Resource = 0x00800000,
			xAttributes = 0x01000000,
			xWeaponAttributes = 0x02000000,
			PlayerConstructed = 0x04000000,
			SkillBonuses = 0x08000000,
			Slayer2 = 0x10000000,
			ElementalDamages = 0x20000000,
			EngravedText = 0x40000000,
			xAbsorptionAttributes = 0x80000000,
            xNegativeAttributes = 0x100000000
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			int version = reader.ReadInt();

			switch (version)
			{
                case 14:
                    {
                        goto case 13;
                    }
                case 13:
                case 12:
                    {
                        #region Runic Reforging
                        m_ReforgedPrefix = (ReforgedPrefix)reader.ReadInt();
                        m_ReforgedSuffix = (ReforgedSuffix)reader.ReadInt();
                        m_ItemPower = (ItemPower)reader.ReadInt();
                        m_BlockRepair = reader.ReadBool();
                        #endregion

                        #region Stygian Abyss
                        m_DImodded = reader.ReadBool();
                        m_SearingWeapon = reader.ReadBool();
                        goto case 11;
                    }
				case 11:
					{
						m_TimesImbued = reader.ReadInt();
                     
                        #endregion

                        goto case 10;
					}
				case 10:
					{
						m_BlessedBy = reader.ReadMobile();
						m_EngravedText = reader.ReadString();
						goto case 5;
					}
				case 9:
				case 8:
				case 7:
				case 6:
				case 5:
					{
						SaveFlag flags;
                        
                        if(version < 13)
                            flags = (SaveFlag)reader.ReadInt();
                        else
                            flags = (SaveFlag)reader.ReadLong();

						if (GetSaveFlag(flags, SaveFlag.DamageLevel))
						{
							m_DamageLevel = (WeaponDamageLevel)reader.ReadInt();

							if (m_DamageLevel > WeaponDamageLevel.Vanq)
							{
								m_DamageLevel = WeaponDamageLevel.Ruin;
							}
						}

						if (GetSaveFlag(flags, SaveFlag.AccuracyLevel))
						{
							m_AccuracyLevel = (WeaponAccuracyLevel)reader.ReadInt();

							if (m_AccuracyLevel > WeaponAccuracyLevel.Supremely)
							{
								m_AccuracyLevel = WeaponAccuracyLevel.Accurate;
							}
						}

						if (GetSaveFlag(flags, SaveFlag.DurabilityLevel))
						{
							m_DurabilityLevel = (WeaponDurabilityLevel)reader.ReadInt();

							if (m_DurabilityLevel > WeaponDurabilityLevel.Indestructible)
							{
								m_DurabilityLevel = WeaponDurabilityLevel.Durable;
							}
						}

						if (GetSaveFlag(flags, SaveFlag.Quality))
						{
							m_Quality = (WeaponQuality)reader.ReadInt();
						}
						else
						{
							m_Quality = WeaponQuality.Regular;
						}

						if (GetSaveFlag(flags, SaveFlag.Hits))
						{
							m_Hits = reader.ReadInt();
						}

						if (GetSaveFlag(flags, SaveFlag.MaxHits))
						{
							m_MaxHits = reader.ReadInt();
						}

						if (GetSaveFlag(flags, SaveFlag.Slayer))
						{
							m_Slayer = (SlayerName)reader.ReadInt();
						}

						if (GetSaveFlag(flags, SaveFlag.Poison))
						{
							m_Poison = Poison.Deserialize(reader);
						}

						if (GetSaveFlag(flags, SaveFlag.PoisonCharges))
						{
							m_PoisonCharges = reader.ReadInt();
						}

						if (GetSaveFlag(flags, SaveFlag.Crafter))
						{
							m_Crafter = reader.ReadMobile();
						}

						if (GetSaveFlag(flags, SaveFlag.Identified))
						{
							m_Identified = (version >= 6 || reader.ReadBool());
						}

						if (GetSaveFlag(flags, SaveFlag.StrReq))
						{
							m_StrReq = reader.ReadInt();
						}
						else
						{
							m_StrReq = -1;
						}

						if (GetSaveFlag(flags, SaveFlag.DexReq))
						{
							m_DexReq = reader.ReadInt();
						}
						else
						{
							m_DexReq = -1;
						}

						if (GetSaveFlag(flags, SaveFlag.IntReq))
						{
							m_IntReq = reader.ReadInt();
						}
						else
						{
							m_IntReq = -1;
						}

						if (GetSaveFlag(flags, SaveFlag.MinDamage))
						{
							m_MinDamage = reader.ReadInt();
						}
						else
						{
							m_MinDamage = -1;
						}

						if (GetSaveFlag(flags, SaveFlag.MaxDamage))
						{
							m_MaxDamage = reader.ReadInt();
						}
						else
						{
							m_MaxDamage = -1;
						}

						if (GetSaveFlag(flags, SaveFlag.HitSound))
						{
							m_HitSound = reader.ReadInt();
						}
						else
						{
							m_HitSound = -1;
						}

						if (GetSaveFlag(flags, SaveFlag.MissSound))
						{
							m_MissSound = reader.ReadInt();
						}
						else
						{
							m_MissSound = -1;
						}

						if (GetSaveFlag(flags, SaveFlag.Speed))
						{
							if (version < 9)
							{
								m_Speed = reader.ReadInt();
							}
							else
							{
								m_Speed = reader.ReadFloat();
							}
						}
						else
						{
							m_Speed = -1;
						}

						if (GetSaveFlag(flags, SaveFlag.MaxRange))
						{
							m_MaxRange = reader.ReadInt();
						}
						else
						{
							m_MaxRange = -1;
						}

						if (GetSaveFlag(flags, SaveFlag.Skill))
						{
							m_Skill = (SkillName)reader.ReadInt();
						}
						else
						{
							m_Skill = (SkillName)(-1);
						}

						if (GetSaveFlag(flags, SaveFlag.Type))
						{
							m_Type = (WeaponType)reader.ReadInt();
						}
						else
						{
							m_Type = (WeaponType)(-1);
						}

						if (GetSaveFlag(flags, SaveFlag.Animation))
						{
							m_Animation = (WeaponAnimation)reader.ReadInt();
						}
						else
						{
							m_Animation = (WeaponAnimation)(-1);
						}

						if (GetSaveFlag(flags, SaveFlag.Resource))
						{
							m_Resource = (CraftResource)reader.ReadInt();
						}
						else
						{
							m_Resource = CraftResource.Iron;
						}

						if (UseSkillMod && m_AccuracyLevel != WeaponAccuracyLevel.Regular && Parent is Mobile)
						{
							m_SkillMod = new DefaultSkillMod(AccuracySkill, true, (int)m_AccuracyLevel * 5);
							((Mobile)Parent).AddSkillMod(m_SkillMod);
						}

						if (GetSaveFlag(flags, SaveFlag.PlayerConstructed))
						{
							m_PlayerConstructed = true;
						}

						if (GetSaveFlag(flags, SaveFlag.Slayer2))
						{
							m_Slayer2 = (SlayerName)reader.ReadInt();
						}

						if (GetSaveFlag(flags, SaveFlag.EngravedText))
						{
							m_EngravedText = reader.ReadString();
						}

						break;
					}
				case 4:
					{
						m_Slayer = (SlayerName)reader.ReadInt();

						goto case 3;
					}
				case 3:
					{
						m_StrReq = reader.ReadInt();
						m_DexReq = reader.ReadInt();
						m_IntReq = reader.ReadInt();

						goto case 2;
					}
				case 2:
					{
						m_Identified = reader.ReadBool();

						goto case 1;
					}
				case 1:
					{
						m_MaxRange = reader.ReadInt();

						goto case 0;
					}
				case 0:
					{
						if (version == 0)
						{
							m_MaxRange = 1; // default
						}

						if (version < 5)
						{
							m_Resource = CraftResource.Iron;
						}

						m_MinDamage = reader.ReadInt();
						m_MaxDamage = reader.ReadInt();

						m_Speed = reader.ReadInt();

						m_HitSound = reader.ReadInt();
						m_MissSound = reader.ReadInt();

						m_Skill = (SkillName)reader.ReadInt();
						m_Type = (WeaponType)reader.ReadInt();
						m_Animation = (WeaponAnimation)reader.ReadInt();
						m_DamageLevel = (WeaponDamageLevel)reader.ReadInt();
						m_AccuracyLevel = (WeaponAccuracyLevel)reader.ReadInt();
						m_DurabilityLevel = (WeaponDurabilityLevel)reader.ReadInt();
						m_Quality = (WeaponQuality)reader.ReadInt();

						m_Crafter = reader.ReadMobile();

						m_Poison = Poison.Deserialize(reader);
						m_PoisonCharges = reader.ReadInt();

						if (m_StrReq == OldStrengthReq)
						{
							m_StrReq = -1;
						}

						if (m_DexReq == OldDexterityReq)
						{
							m_DexReq = -1;
						}

						if (m_IntReq == OldIntelligenceReq)
						{
							m_IntReq = -1;
						}

						if (m_MinDamage == OldMinDamage)
						{
							m_MinDamage = -1;
						}

						if (m_MaxDamage == OldMaxDamage)
						{
							m_MaxDamage = -1;
						}

						if (m_HitSound == OldHitSound)
						{
							m_HitSound = -1;
						}

						if (m_MissSound == OldMissSound)
						{
							m_MissSound = -1;
						}

						if (m_Speed == OldSpeed)
						{
							m_Speed = -1;
						}

						if (m_MaxRange == OldMaxRange)
						{
							m_MaxRange = -1;
						}

						if (m_Skill == OldSkill)
						{
							m_Skill = (SkillName)(-1);
						}

						if (m_Type == OldType)
						{
							m_Type = (WeaponType)(-1);
						}

						if (m_Animation == OldAnimation)
						{
							m_Animation = (WeaponAnimation)(-1);
						}

						if (UseSkillMod && m_AccuracyLevel != WeaponAccuracyLevel.Regular && Parent is Mobile)
						{
							m_SkillMod = new DefaultSkillMod(AccuracySkill, true, (int)m_AccuracyLevel * 5);
							((Mobile)Parent).AddSkillMod(m_SkillMod);
						}

						break;
					}
			}

			int strBonus = 0;
			int dexBonus = 0;
			int intBonus = 0;

			if (Parent is Mobile && (strBonus != 0 || dexBonus != 0 || intBonus != 0))
			{
				Mobile m = (Mobile)Parent;

				string modName = Serial.ToString();

				if (strBonus != 0)
				{
					m.AddStatMod(new StatMod(StatType.Str, modName + "Str", strBonus, TimeSpan.Zero));
				}

				if (dexBonus != 0)
				{
					m.AddStatMod(new StatMod(StatType.Dex, modName + "Dex", dexBonus, TimeSpan.Zero));
				}

				if (intBonus != 0)
				{
					m.AddStatMod(new StatMod(StatType.Int, modName + "Int", intBonus, TimeSpan.Zero));
				}
			}

			if (Parent is Mobile)
			{
				((Mobile)Parent).CheckStatTimers();
			}

			if (m_Hits <= 0 && m_MaxHits <= 0)
			{
				m_Hits = m_MaxHits = Utility.RandomMinMax(InitMinHits, InitMaxHits);
			}

			if (version < 6)
			{
				m_PlayerConstructed = true; // we don't know, so, assume it's crafted
			}
		}
		#endregion

		public BaseWeapon(int itemID)
			: base(itemID)
		{
			Layer = (Layer)ItemData.Quality;

			m_Quality = WeaponQuality.Regular;
			m_StrReq = -1;
			m_DexReq = -1;
			m_IntReq = -1;
			m_MinDamage = -1;
			m_MaxDamage = -1;
			m_HitSound = -1;
			m_MissSound = -1;
			m_Speed = -1;
			m_MaxRange = -1;
			m_Skill = (SkillName)(-1);
			m_Type = (WeaponType)(-1);
			m_Animation = (WeaponAnimation)(-1);

			m_Hits = m_MaxHits = Utility.RandomMinMax(InitMinHits, InitMaxHits);

			m_Resource = CraftResource.Iron;

			// Xml Spawner XmlSockets - SOF
			// mod to randomly add sockets and socketability features to armor. These settings will yield
			// 2% drop rate of socketed/socketable items
			// 0.1% chance of 5 sockets
			// 0.5% of 4 sockets
			// 3% chance of 3 sockets
			// 15% chance of 2 sockets
			// 50% chance of 1 socket
			// the remainder will be 0 socket (31.4% in this case)
			if(XmlSpawner.SocketsEnabled)
				XmlSockets.ConfigureRandom(this, 2.0, 0.1, 0.5, 3.0, 15.0, 50.0);
		}

		public BaseWeapon(Serial serial)
			: base(serial)
		{ }

		private string GetNameString()
		{
			string name = Name;

			if (name == null)
			{
				name = String.Format("#{0}", LabelNumber);
			}

			return name;
		}

		[Hue, CommandProperty(AccessLevel.GameMaster)]
		public override int Hue
		{
			get { return base.Hue; }
			set
			{
				base.Hue = value;
				InvalidateProperties();
			}
		}

		public int GetElementalDamageHue()
		{
			int phys, fire, cold, pois, nrgy, chaos, direct;
			GetDamageTypes(null, out phys, out fire, out cold, out pois, out nrgy, out chaos, out direct);
			//Order is Cold, Energy, Fire, Poison, Physical left

			int currentMax = 50;
			int hue = 0;

			if (pois >= currentMax)
			{
				hue = 1267 + (pois - 50) / 10;
				currentMax = pois;
			}

			if (fire >= currentMax)
			{
				hue = 1255 + (fire - 50) / 10;
				currentMax = fire;
			}

			if (nrgy >= currentMax)
			{
				hue = 1273 + (nrgy - 50) / 10;
				currentMax = nrgy;
			}

			if (cold >= currentMax)
			{
				hue = 1261 + (cold - 50) / 10;
				currentMax = cold;
			}

			return hue;
		}

		public override void AddNameProperty(ObjectPropertyList list)
		{
			int oreType;

			switch (m_Resource)
			{
				case CraftResource.DullCopper:
					oreType = 1053108;
					break; // dull copper
				case CraftResource.ShadowIron:
					oreType = 1053107;
					break; // shadow iron
				case CraftResource.Copper:
					oreType = 1053106;
					break; // copper
				case CraftResource.Bronze:
					oreType = 1053105;
					break; // bronze
				case CraftResource.Gold:
					oreType = 1053104;
					break; // golden
				case CraftResource.Agapite:
					oreType = 1053103;
					break; // agapite
				case CraftResource.Verite:
					oreType = 1053102;
					break; // verite
				case CraftResource.Valorite:
					oreType = 1053101;
					break; // valorite
				case CraftResource.SpinedLeather:
					oreType = 1061118;
					break; // spined
				case CraftResource.HornedLeather:
					oreType = 1061117;
					break; // horned
				case CraftResource.BarbedLeather:
					oreType = 1061116;
					break; // barbed
				case CraftResource.RedScales:
					oreType = 1060814;
					break; // red
				case CraftResource.YellowScales:
					oreType = 1060818;
					break; // yellow
				case CraftResource.BlackScales:
					oreType = 1060820;
					break; // black
				case CraftResource.GreenScales:
					oreType = 1060819;
					break; // green
				case CraftResource.WhiteScales:
					oreType = 1060821;
					break; // white
				case CraftResource.BlueScales:
					oreType = 1060815;
					break; // blue

				default:
					oreType = 0;
					break;
			}

			if (oreType != 0)
			{
				list.Add(1053099, "#{0}\t{1}", oreType, GetNameString()); // ~1_oretype~ ~2_armortype~
            }
            else if (Name == null)
            {
                list.Add(LabelNumber);
            }
            else
            {
                list.Add(Name);
            }

			/*
            * Want to move this to the engraving tool, let the non-harmful 
            * formatting show, and remove CLILOCs embedded: more like OSI
            * did with the books that had markup, etc.
            * 
            * This will have a negative effect on a few event things imgame 
            * as is.
            * 
            * If we cant find a more OSI-ish way to clean it up, we can 
            * easily put this back, and use it in the deserialize
            * method and engraving tool, to make it perm cleaned up.
            */

			if (!String.IsNullOrEmpty(m_EngravedText))
			{
				list.Add(1062613, m_EngravedText);
			}
			/* list.Add( 1062613, Utility.FixHtml( m_EngravedText ) ); */
		}

		public override bool AllowEquipedCast(Mobile from)
		{
			if (base.AllowEquipedCast(from))
			{
				return true;
			}

			return false;
		}

		public virtual int ArtifactRarity { get { return 0; } }

		public virtual int GetLuckBonus()
		{

			CraftResourceInfo resInfo = CraftResources.GetInfo(m_Resource);

			if (resInfo == null)
			{
				return 0;
			}

			CraftAttributeInfo attrInfo = resInfo.AttributeInfo;

			if (attrInfo == null)
			{
				return 0;
			}

			return attrInfo.WeaponLuck;
		}

		public override void GetProperties(ObjectPropertyList list)
		{
			base.GetProperties(list);

			XmlLevelItem levitem = XmlAttach.FindAttachment(this, typeof(XmlLevelItem)) as XmlLevelItem;

			if (levitem != null)
			{
				list.Add(1060658, "Level\t{0}", levitem.Level);

				if (LevelItems.DisplayExpProp)
				{
					list.Add(1060659, "Experience\t{0}", levitem.Experience);
				}
			}
           

			if (IsImbued)
			{
				list.Add(1080418); // (Imbued)
			}

			if (m_Crafter != null)
			{
				list.Add(1050043, m_Crafter.TitleName); // crafted by ~1_NAME~
			}

			#region Factions
			if (m_FactionState != null)
			{
				list.Add(1041350); // faction item
			}
			#endregion

			if (m_Quality == WeaponQuality.Exceptional)
			{
				list.Add(1060636); // exceptional
			}

			if (RequiredRace == Race.Elf)
			{
				list.Add(1075086); // Elves Only
			}

			if (ArtifactRarity > 0)
			{
				list.Add(1061078, ArtifactRarity.ToString()); // artifact rarity ~1_val~
			}

			if (this is IUsesRemaining && ((IUsesRemaining)this).ShowUsesRemaining)
			{
				list.Add(1060584, ((IUsesRemaining)this).UsesRemaining.ToString()); // uses remaining: ~1_val~
			}

			if (m_Slayer != SlayerName.None)
			{
				SlayerEntry entry = SlayerGroup.GetEntryByName(m_Slayer);
				if (entry != null)
				{
					list.Add(entry.Title);
				}
			}

			if (m_Slayer2 != SlayerName.None)
			{
				SlayerEntry entry = SlayerGroup.GetEntryByName(m_Slayer2);
				if (entry != null)
				{
					list.Add(entry.Title);
				}
			}

			base.AddResistanceProperties(list);

            double focusBonus = 1;
            int enchantBonus = 0;
            bool fcMalus = false;
            int damBonus = 0;
            SpecialMove move = null;

            int prop;
            double fprop;

			int phys, fire, cold, pois, nrgy, chaos, direct;

			if (phys != 0)
			{
				list.Add(1060403, phys.ToString()); // physical damage ~1_val~%
			}

			if (fire != 0)
			{
				list.Add(1060405, fire.ToString()); // fire damage ~1_val~%
			}

			if (cold != 0)
			{
				list.Add(1060404, cold.ToString()); // cold damage ~1_val~%
			}

			if (pois != 0)
			{
				list.Add(1060406, pois.ToString()); // poison damage ~1_val~%
			}

			if (nrgy != 0)
			{
				list.Add(1060407, nrgy.ToString()); // energy damage ~1_val
			}

			if (Core.ML && chaos != 0)
			{
				list.Add(1072846, chaos.ToString()); // chaos damage ~1_val~%
			}

			if (Core.ML && direct != 0)
			{
				list.Add(1079978, direct.ToString()); // Direct Damage: ~1_PERCENT~%
            }

            list.Add(1061168, "{0}\t{1}", MinDamage.ToString(), MaxDamage.ToString()); // weapon damage ~1_val~ - ~2_val~

			if (Core.ML)
			{
				list.Add(1061167, String.Format("{0}s", Speed)); // weapon speed ~1_val~
			}
			else
			{
				list.Add(1061167, Speed.ToString());
			}

			if (MaxRange > 1)
			{
				list.Add(1061169, MaxRange.ToString()); // range ~1_val~
			}

			int strReq = StrRequirement;

			if (strReq > 0)
			{
				list.Add(1061170, strReq.ToString()); // strength requirement ~1_val~
			}

			if (Layer == Layer.TwoHanded)
			{
				list.Add(1061171); // two-handed weapon
			}
			else
			{
				list.Add(1061824); // one-handed weapon
			}

			XmlAttach.AddAttachmentProperties(this, list);

			if (m_Hits >= 0 && m_MaxHits > 0)
			{
				list.Add(1060639, "{0}\t{1}", m_Hits, m_MaxHits); // durability ~1_val~ / ~2_val~
			}
        }

        public override void OnSingleClick(Mobile from)
		{
			var attrs = new List<EquipInfoAttribute>();

			if (DisplayLootType)
			{
				if (LootType == LootType.Blessed)
				{
					attrs.Add(new EquipInfoAttribute(1038021)); // blessed
				}
				else if (LootType == LootType.Cursed)
				{
					attrs.Add(new EquipInfoAttribute(1049643)); // cursed
				}
			}

			#region Factions
			if (m_FactionState != null)
			{
				attrs.Add(new EquipInfoAttribute(1041350)); // faction item
			}
			#endregion

			if (m_Quality == WeaponQuality.Exceptional)
			{
				attrs.Add(new EquipInfoAttribute(1018305 - (int)m_Quality));
			}

			if (m_Identified || from.AccessLevel >= AccessLevel.GameMaster)
			{
				if (m_Slayer != SlayerName.None)
				{
					SlayerEntry entry = SlayerGroup.GetEntryByName(m_Slayer);
					if (entry != null)
					{
						attrs.Add(new EquipInfoAttribute(entry.Title));
					}
				}

				if (m_Slayer2 != SlayerName.None)
				{
					SlayerEntry entry = SlayerGroup.GetEntryByName(m_Slayer2);
					if (entry != null)
					{
						attrs.Add(new EquipInfoAttribute(entry.Title));
					}
				}

				if (m_DurabilityLevel != WeaponDurabilityLevel.Regular)
				{
					attrs.Add(new EquipInfoAttribute(1038000 + (int)m_DurabilityLevel));
				}

				if (m_DamageLevel != WeaponDamageLevel.Regular)
				{
					attrs.Add(new EquipInfoAttribute(1038015 + (int)m_DamageLevel));
				}

				if (m_AccuracyLevel != WeaponAccuracyLevel.Regular)
				{
					attrs.Add(new EquipInfoAttribute(1038010 + (int)m_AccuracyLevel));
				}
			}
			else if (m_Slayer != SlayerName.None || m_Slayer2 != SlayerName.None ||
					 m_DurabilityLevel != WeaponDurabilityLevel.Regular || m_DamageLevel != WeaponDamageLevel.Regular ||
					 m_AccuracyLevel != WeaponAccuracyLevel.Regular)
			{
				attrs.Add(new EquipInfoAttribute(1038000)); // Unidentified
			}

			if (m_Poison != null && m_PoisonCharges > 0)
			{
				attrs.Add(new EquipInfoAttribute(1017383, m_PoisonCharges));
			}

			int number;

			if (Name == null)
			{
				number = LabelNumber;
			}
			else
			{
				LabelTo(from, Name);
				number = 1041000;
			}

			if (attrs.Count == 0 && Crafter == null && Name != null)
			{
				return;
			}

			EquipmentInfo eqInfo = new EquipmentInfo(number, m_Crafter, false, attrs.ToArray());

			from.Send(new DisplayEquipmentInfo(this, eqInfo));
		}

		public static BaseWeapon Fists { get; set; }

		#region ICraftable Members
		public int OnCraft(
			int quality,
			bool makersMark,
			Mobile from,
			CraftSystem craftSystem,
			Type typeRes,
			BaseTool tool,
			CraftItem craftItem,
			int resHue)
		{
			Quality = (WeaponQuality)quality;

			if (makersMark)
			{
				Crafter = from;
			}

			PlayerConstructed = true;

			if (typeRes == null)
			{
				typeRes = craftItem.Resources.GetAt(0).ItemType;
			}

			if (Core.AOS)
			{
				if (!craftItem.ForceNonExceptional)
				{
					Resource = CraftResources.GetFromType(typeRes);
				}

				CraftContext context = craftSystem.GetContext(from);

				if (context != null && context.DoNotColor)
				{
					Hue = 0;
				}
			}

			if (craftItem != null && !craftItem.ForceNonExceptional)
			{
				CraftResourceInfo resInfo = CraftResources.GetInfo(m_Resource);

				if (resInfo == null)
				{
					return quality;
				}

				CraftAttributeInfo attrInfo = resInfo.AttributeInfo;

				if (attrInfo == null)
				{
					return quality;
				}

			}
			#endregion

			return quality;
		}
	}

	public enum CheckSlayerResult
	{
		None,
		Slayer,
        	SuperSlayer,
		Opposition
	}
}
