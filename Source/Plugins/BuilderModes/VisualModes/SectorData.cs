#region === Copyright (c) 2010 Pascal van der Heiden ===

using System.Collections.Generic;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Rendering;

#endregion

namespace CodeImp.DoomBuilder.BuilderModes
{
	internal class SectorData
	{
		#region ================== Variables
		
		// VisualMode
		private readonly BaseVisualMode mode;
		
		// Sector for which this data is
		private readonly Sector sector;
		
		// Levels have been updated?
		private bool updated;
		
		// This prevents recursion
		private bool isupdating;
		
		// All planes in the sector that cast or are affected by light
		private readonly List<SectorLevel> lightlevels;
		
		// Effects
		private readonly List<SectorEffect> alleffects;
		private readonly List<Effect3DFloor> extrafloors;
		
		// Sectors that must be updated when this sector is changed
		// The boolean value is the 'includeneighbours' of the UpdateSectorGeometry function which
		// indicates if the sidedefs of neighbouring sectors should also be rebuilt.
		private readonly Dictionary<Sector, bool> updatesectors;
		
		// Original floor and ceiling levels
		private readonly SectorLevel floor;
		private readonly SectorLevel ceiling;
		
		// This helps keeping track of changes
		// otherwise we update ceiling/floor too much
		private bool floorchanged;
		private bool ceilingchanged;

		#endregion
		
		#region ================== Properties
		
		public Sector Sector { get { return sector; } }
		public bool Updated { get { return updated; } }
		public bool FloorChanged { get { return floorchanged; } set { floorchanged |= value; } }
		public bool CeilingChanged { get { return ceilingchanged; } set { ceilingchanged |= value; } }
		public List<SectorLevel> LightLevels { get { return lightlevels; } }
		public List<Effect3DFloor> ExtraFloors { get { return extrafloors; } }
		public SectorLevel Floor { get { return floor; } }
		public SectorLevel Ceiling { get { return ceiling; } }
		public BaseVisualMode Mode { get { return mode; } }
		public Dictionary<Sector, bool> UpdateAlso { get { return updatesectors; } }
		
		#endregion
		
		#region ================== Constructor / Destructor
		
		// Constructor
		public SectorData(BaseVisualMode mode, Sector s)
		{
			// Initialize
			this.mode = mode;
			this.sector = s;
			this.updated = false;
			this.floorchanged = false;
			this.ceilingchanged = false;
			this.lightlevels = new List<SectorLevel>(2);
			this.extrafloors = new List<Effect3DFloor>(1);
			this.alleffects = new List<SectorEffect>(1);
			this.updatesectors = new Dictionary<Sector, bool>(2);
			this.floor = new SectorLevel(sector, SectorLevelType.Floor);
			this.ceiling = new SectorLevel(sector, SectorLevelType.Ceiling);
			
			BasicSetup();
			
			// Add ceiling and floor
			lightlevels.Add(floor);
			lightlevels.Add(ceiling);
		}
		
		#endregion
		
		#region ================== Public Methods
		
		// 3D Floor effect
		public void AddEffect3DFloor(Linedef sourcelinedef)
		{
			Effect3DFloor e = new Effect3DFloor(this, sourcelinedef);
			extrafloors.Add(e);
			alleffects.Add(e);
		}
		
		// Brightness level effect
		public void AddEffectBrightnessLevel(Linedef sourcelinedef)
		{
			EffectBrightnessLevel e = new EffectBrightnessLevel(this, sourcelinedef);
			alleffects.Add(e);
		}

		//mxd. Transfer Floor Brightness effect
		public void AddEffectTransferFloorBrightness(Linedef sourcelinedef) 
		{
			EffectTransferFloorBrightness e = new EffectTransferFloorBrightness(this, sourcelinedef);
			alleffects.Add(e);
		}

		//mxd. Transfer Floor Brightness effect
		public void AddEffectTransferCeilingBrightness(Linedef sourcelinedef) 
		{
			EffectTransferCeilingBrightness e = new EffectTransferCeilingBrightness(this, sourcelinedef);
			alleffects.Add(e);
		}

		// Line slope effect
		public void AddEffectLineSlope(Linedef sourcelinedef)
		{
			EffectLineSlope e = new EffectLineSlope(this, sourcelinedef);
			alleffects.Add(e);
		}

		//mxd. Plane copy slope effect
		public void AddEffectPlaneClopySlope(Linedef sourcelinedef, bool front) 
		{
			EffectPlaneCopySlope e = new EffectPlaneCopySlope(this, sourcelinedef, front);
			alleffects.Add(e);
		}

		// Copy slope effect
		public void AddEffectCopySlope(Thing sourcething)
		{
			EffectCopySlope e = new EffectCopySlope(this, sourcething);
			alleffects.Add(e);
		}

		// Thing line slope effect
		public void AddEffectThingLineSlope(Thing sourcething)
		{
			EffectThingLineSlope e = new EffectThingLineSlope(this, sourcething);
			alleffects.Add(e);
		}

		// Thing slope effect
		public void AddEffectThingSlope(Thing sourcething)
		{
			EffectThingSlope e = new EffectThingSlope(this, sourcething);
			alleffects.Add(e);
		}

		// Thing vertex slope effect
		public void AddEffectThingVertexSlope(List<Thing> sourcethings, bool slopefloor)
		{
			EffectThingVertexSlope e = new EffectThingVertexSlope(this, sourcethings, slopefloor);
			alleffects.Add(e);
		}

		//mxd. Add UDMF vertex offset effect
		public void AddEffectVertexOffset() 
		{
			EffectUDMFVertexOffset e = new EffectUDMFVertexOffset(this);
			alleffects.Add(e);
		}
		
		// This adds a sector for updating
		public void AddUpdateSector(Sector s, bool includeneighbours)
		{
			updatesectors[s] = includeneighbours;
		}
		
		// This adds a sector level
		public void AddSectorLevel(SectorLevel level)
		{
			// Note: Inserting before the end so that the ceiling stays
			// at the end and the floor at the beginning
			lightlevels.Insert(lightlevels.Count - 1, level);
		}
		
		// This resets this sector data and all sectors that require updating after me
		public void Reset()
		{
			if(isupdating)
				return;

			isupdating = true;

			// This is set to false so that this sector is rebuilt the next time it is needed!
			updated = false;

			// The visual sector associated is now outdated
			if(mode.VisualSectorExists(sector))
			{
				BaseVisualSector vs = (BaseVisualSector)mode.GetVisualSector(sector);
				vs.UpdateSectorGeometry(false);
			}
			
			// Also reset the sectors that depend on this sector
			foreach(KeyValuePair<Sector, bool> s in updatesectors)
			{
				SectorData sd = mode.GetSectorData(s.Key);
				sd.Reset();
			}

			isupdating = false;
		}

		// This sets up the basic floor and ceiling, as they would be in normal Doom circumstances
		private void BasicSetup()
		{
			//mxd
			if(sector.FloorSlope.GetLengthSq() > 0 && !float.IsNaN(sector.FloorSlopeOffset / sector.FloorSlope.z)) 
			{
				// Sloped plane
				floor.plane = new Plane(sector.FloorSlope, sector.FloorSlopeOffset);
			} 
			else 
			{
				// Normal (flat) floor plane
				floor.plane = new Plane(new Vector3D(0, 0, 1), -sector.FloorHeight);
			}

			if(sector.CeilSlope.GetLengthSq() > 0 && !float.IsNaN(sector.CeilSlopeOffset / sector.CeilSlope.z)) 
			{
				// Sloped plane
				ceiling.plane = new Plane(sector.CeilSlope, sector.CeilSlopeOffset);
			} 
			else 
			{
				// Normal (flat) ceiling plane
				ceiling.plane = new Plane(new Vector3D(0, 0, -1), sector.CeilHeight);
			}
			
			// Fetch ZDoom fields
			int color = sector.Fields.GetValue("lightcolor", -1);
			int flight = sector.Fields.GetValue("lightfloor", 0);
			bool fabs = sector.Fields.GetValue("lightfloorabsolute", false);
			int clight = sector.Fields.GetValue("lightceiling", 0);
			bool cabs = sector.Fields.GetValue("lightceilingabsolute", false);

			// Determine colors & light levels
			PixelColor lightcolor = PixelColor.FromInt(color);
			if(!fabs) flight = sector.Brightness + flight;
			if(!cabs) clight = sector.Brightness + clight;
			PixelColor floorbrightness = PixelColor.FromInt(mode.CalculateBrightness(flight));
			PixelColor ceilingbrightness = PixelColor.FromInt(mode.CalculateBrightness(clight));
			PixelColor floorcolor = PixelColor.Modulate(lightcolor, floorbrightness);
			PixelColor ceilingcolor = PixelColor.Modulate(lightcolor, ceilingbrightness);
			floor.color = floorcolor.WithAlpha(255).ToInt();
			floor.brightnessbelow = sector.Brightness;
			floor.colorbelow = lightcolor.WithAlpha(255);
			ceiling.color = ceilingcolor.WithAlpha(255).ToInt();
			ceiling.brightnessbelow = sector.Brightness;
			ceiling.colorbelow = lightcolor.WithAlpha(255);
		}

		//mxd
		public void UpdateForced() 
		{
			updated = false;
			Update();
		}

		// When no geometry has been changed and no effects have been added or removed,
		// you can call this again to update existing effects. The effects will update
		// the existing SectorLevels to match with any changes.
		public void Update()
		{
			if(isupdating || updated) return;
			isupdating = true;
			
			// Set floor/ceiling to their original setup
			BasicSetup();

			// Update all effects
			foreach(SectorEffect e in alleffects)
				e.Update();
			
			// Sort the levels (only if there are more than 2 sector levels - mxd)
			if (lightlevels.Count > 2) 
			{
				SectorLevelComparer comparer = new SectorLevelComparer(sector);
				lightlevels.Sort(0, lightlevels.Count, comparer); //mxd. Was lightlevels.Sort(1, lightlevels.Count - 2, comparer); 
			}

			//mxd. 3d floors can be above the real ceiling, so let's find it first... 
			int startindex = lightlevels.Count - 2;
			for(int i = lightlevels.Count - 2; i >= 0; i--)
			{
				if(lightlevels[i].type == SectorLevelType.Ceiling && lightlevels[i].sector.Index == sector.Index)
				{
					startindex = i;
					break;
				}
			}

			// Now that we know the levels in this sector (and in the right order) we
			// can determine the lighting in between and on the levels.
			// Start from the absolute ceiling and go down to 'cast' the lighting
			SectorLevel stored = ceiling; //mxd
			for(int i = startindex; i >= 0; i--)
			{
				SectorLevel l = lightlevels[i];
				SectorLevel pl = lightlevels[i + 1];

				if(l.lighttype == LightLevelType.TYPE1) stored = pl; //mxd

				//mxd. If the real floor has "lightfloor" value and the 3d floor above it doesn't cast down light, use real floor's brightness
				if(General.Map.UDMF && l == floor && lightlevels.Count > 2 && (pl.disablelighting || pl.restrictlighting) && l.sector.Fields.ContainsKey("lightfloor"))
				{
					int light = l.sector.Fields.GetValue("lightfloor", pl.brightnessbelow);
					pl.brightnessbelow = (l.sector.Fields.GetValue("lightfloorabsolute", false) ? light : l.sector.Brightness + light);
				}
				
				// Set color when no color is specified, or when a 3D floor is placed above the absolute floor
				//mxd. Or when lightlevel is above a floor/ceiling level
				bool uselightlevellight = ((l.type != SectorLevelType.Light) && pl != null && pl.type == SectorLevelType.Light); //mxd
				if((l.color == 0) || ((l == floor) && (lightlevels.Count > 2)) || uselightlevellight)
				{
					PixelColor floorbrightness = PixelColor.FromInt(mode.CalculateBrightness(pl.brightnessbelow));
					PixelColor floorcolor = PixelColor.Modulate(pl.colorbelow, floorbrightness);
					l.color = floorcolor.WithAlpha(255).ToInt();

					if(uselightlevellight) l.brightnessbelow = pl.brightnessbelow;
				}
				//mxd. Bottom TYPE1 border requires special handling...
				else if(l.lighttype == LightLevelType.TYPE1_BOTTOM)
				{
					//Use brightness and color from previous light level when it's between TYPE1 and TYPE1_BOTTOM levels
					if(pl.type == SectorLevelType.Light && pl.lighttype != LightLevelType.TYPE1)
					{
						l.brightnessbelow = pl.brightnessbelow;
						l.colorbelow = pl.colorbelow;
					}
					//Use brightness and color from the light level above TYPE1 level
					else if(stored.type == SectorLevelType.Light)
					{
						l.brightnessbelow = stored.brightnessbelow;
						l.colorbelow = stored.colorbelow;
					}
					// Otherwise light values from the real ceiling are used 
				}

				if(l.colorbelow.a == 0) l.colorbelow = pl.colorbelow;
				if(l.brightnessbelow == -1) l.brightnessbelow = pl.brightnessbelow;
			}

			floorchanged = false;
			ceilingchanged = false;
			updated = true;
			isupdating = false;
		}

		// This returns the level above the given point
		public SectorLevel GetLevelAbove(Vector3D pos)
		{
			SectorLevel found = null;
			float dist = float.MaxValue;
			
			foreach(SectorLevel l in lightlevels)
			{
				float d = l.plane.GetZ(pos) - pos.z;
				if((d > 0.0f) && (d < dist))
				{
					dist = d;
					found = l;
				}
			}
			
			return found;
		}

		//mxd. This returns the level above the given point or the level given point is located on
		public SectorLevel GetLevelAboveOrAt(Vector3D pos)
		{
			SectorLevel found = null;
			float dist = float.MaxValue;

			foreach(SectorLevel l in lightlevels) 
			{
				float d = l.plane.GetZ(pos) - pos.z;
				if((d >= 0.0f) && (d < dist)) 
				{
					dist = d;
					found = l;
				}
			}

			return found;
		}

		// This returns the level above the given point
		public SectorLevel GetCeilingAbove(Vector3D pos)
		{
			SectorLevel found = null;
			float dist = float.MaxValue;

			foreach(SectorLevel l in lightlevels)
			{
				if(l.type == SectorLevelType.Ceiling)
				{
					float d = l.plane.GetZ(pos) - pos.z;
					if((d > 0.0f) && (d < dist))
					{
						dist = d;
						found = l;
					}
				}
			}

			return found;
		}

		// This returns the level below the given point
		public SectorLevel GetLevelBelow(Vector3D pos)
		{
			SectorLevel found = null;
			float dist = float.MaxValue;

			foreach(SectorLevel l in lightlevels)
			{
				float d = pos.z - l.plane.GetZ(pos);
				if((d > 0.0f) && (d < dist))
				{
					dist = d;
					found = l;
				}
			}

			return found;
		}

		// This returns the floor below the given point
		public SectorLevel GetFloorBelow(Vector3D pos)
		{
			SectorLevel found = null;
			float dist = float.MaxValue;

			foreach(SectorLevel l in lightlevels)
			{
				if(l.type == SectorLevelType.Floor)
				{
					float d = pos.z - l.plane.GetZ(pos);
					if((d > 0.0f) && (d < dist))
					{
						dist = d;
						found = l;
					}
				}
			}

			return found;
		}

		
		#endregion
	}
}
