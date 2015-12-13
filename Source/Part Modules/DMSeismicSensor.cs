﻿
#region license
/* DMagic Orbital Science - Seismic Sensor
 * Science Module For Seismic Sensor Pod
 *
 * Copyright (c) 2014, David Grandy <david.grandy@gmail.com>
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without modification, 
 * are permitted provided that the following conditions are met:
 * 
 * 1. Redistributions of source code must retain the above copyright notice, 
 * this list of conditions and the following disclaimer.
 * 
 * 2. Redistributions in binary form must reproduce the above copyright notice, 
 * this list of conditions and the following disclaimer in the documentation and/or other materials 
 * provided with the distribution.
 * 
 * 3. Neither the name of the copyright holder nor the names of its contributors may be used 
 * to endorse or promote products derived from this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
 * GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF 
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT 
 * OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DMagic.Part_Modules
{
	public class DMSeismicSensor : DMBasicScienceModule, IDMSeismometer
	{
		[KSPField(isPersistant = true)]
		public float baseExperimentValue = 0.2f;
		[KSPField(guiActive = false)]
		public string scoreString = "0%";

		private string failMessage;
		private DMSeismometerValues values;
		private Material scoreLightOne;
		private Material scoreLightTwo;
		private Material scoreLightThree;
		private Material scoreLightFour;
		private Material scoreLightFive;
		private Material signalLightOne;
		private Color redLight = new Color(0.7647f, 0, 0, 1);
		private Color yellowLight = new Color(0.72f, 0.7137f, 0.0314f, 1);
		private Color greenLight = new Color(0.0549f, 0.7137f, 0.0314f, 1);
		private Color offColor = new Color(0, 0, 0, 0);

		public override void OnStart(PartModule.StartState state)
		{
			base.OnStart(state);

			if (state == StartState.Editor)
				return;

			if (IsDeployed)
				Fields["scoreString"].guiActive = true;
			else
				Fields["scoreString"].guiActive = false;

			Fields["scoreString"].guiName = "Experiment Value";

			GameEvents.onVesselWasModified.Add(onVesselModified);
			GameEvents.onStageSeparation.Add(onDecouple);
			GameEvents.onVesselCreate.Add(onNewVessel);
			GameEvents.onPartCouple.Add(onCouple);

			Transform l1 = part.FindModelTransform("SignalLight_004");
			Transform l2 = part.FindModelTransform("SignalLight_003");
			Transform l3 = part.FindModelTransform("SignalLight_002");
			Transform l4 = part.FindModelTransform("SignalLight_001");
			Transform l5 = part.FindModelTransform("SignalLight_000");
			Transform s1 = part.FindModelTransform("SignalLight01");

			if (l1 != null && l1.renderer != null)
				scoreLightOne = l1.renderer.material;
			if (l2 != null && l2.renderer != null)
				scoreLightTwo = l2.renderer.material;
			if (l3 != null && l3.renderer != null)
				scoreLightThree = l3.renderer.material;
			if (l4 != null && l4.renderer != null)
				scoreLightFour = l4.renderer.material;
			if (l4 != null && l5.renderer != null)
				scoreLightFive = l5.renderer.material;
			if (s1 != null && s1.renderer != null)
				signalLightOne = s1.renderer.material;
		}

		public override string GetInfo()
		{
			string info = base.GetInfo();

			string ranges = string.Format("\nIdeal Seismic Pod Ranges:\nNear: {0:N0}m - {1:N0}m\nFar: {2:N0}m - {3:N0}m", DMSeismicHandler.nearPodThreshold, DMSeismicHandler.nearPodMaxDistance, DMSeismicHandler.farPodThreshold, DMSeismicHandler.farPodMaxDistance);

			string angles = string.Format("\nIdeal Seismic Pod Angle Difference: {0:N0}° - 180°", DMSeismicHandler.podAngleThreshold);

			return info + ranges + angles; ;
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
		}

		public override void OnSave(ConfigNode node)
		{
			base.OnSave(node);
		}

		private void OnDestroy()
		{
			GameEvents.onVesselWasModified.Remove(onVesselModified);
			GameEvents.onStageSeparation.Remove(onDecouple);
			GameEvents.onVesselCreate.Remove(onNewVessel);
			GameEvents.onPartCouple.Remove(onCouple);
		}

		private void onDecouple(EventReport e)
		{
			if (e.origin != part)
				return;

			if (values == null)
				return;

			if (!values.Armed)
				deployEvent();
		}

		private void onCouple(GameEvents.FromToAction<Part, Part> p)
		{
			if (p.from == null)
				return;

			if (p.to == null)
				return;

			if (p.from != part)
				return;

			DMUtils.DebugLog("This part coupled...");

			StartCoroutine(waitOnVessel());
		}

		private IEnumerator waitOnVessel()
		{
			int timer = 0;

			DMSeismicHandler.Instance.removeSeismometer(part.flightID);

			while (timer < 20)
			{
				timer++;
				yield return null;
			}
			
			DMSeismicHandler.Instance.addLoadedSeismometer(part.flightID, this);
		}

		private void onNewVessel(Vessel v)
		{
			if (v == null)
				return;

			if (!v.loaded)
				return;

			if (v != vessel)
				return;

			if (values == null)
				return;

			DMUtils.DebugLog("Assigning New Vessel Ref To Seismic Sensor: {0}", v.vesselName);

			values.VesselRef = v;

			if (!values.Armed)
				deployEvent();
		}

		private void onVesselModified(Vessel v)
		{
			if (v == null)
				return;

			if (vessel != v)
				return;

			if (values == null)
				return;

			DMUtils.DebugLog("This vessel modified...");

			values.OnAsteroid = DMAsteroidScience.AsteroidGrappled;
		}

		private void Update()
		{
			base.EventsCheck();

			if (!HighLogic.LoadedSceneIsFlight)
				return;

			if (values == null)
			{
				values = DMSeismicHandler.Instance.getSeismicSensor(part.flightID);
				if (values != null)
					values.OnAsteroid = DMAsteroidScience.AsteroidGrappled;
				return;
			}

			bool valid = vessel.Landed || values.OnAsteroid;

			if (valid)
				scoreString = values.Score.ToString("P0");
			else
				scoreString = "Not Valid";

			if (values.Armed && values.NearbySensorCount >= 1)
				setEmissive(signalLightOne, greenLight);
			else
				setEmissive(signalLightOne, offColor);

			if (!values.Armed || !valid)
			{
				setEmissive(scoreLightOne, offColor);
				setEmissive(scoreLightTwo, offColor);
				setEmissive(scoreLightThree, offColor);
				setEmissive(scoreLightFour, offColor);
				setEmissive(scoreLightFive, offColor);
			}
			else if (values.Score < 0.21f)
			{
				setEmissive(scoreLightOne, redLight);
				setEmissive(scoreLightTwo, offColor);
				setEmissive(scoreLightThree, offColor);
				setEmissive(scoreLightFour, offColor);
				setEmissive(scoreLightFive, offColor);
			}
			else if (values.Score < 0.56f)
			{
				setEmissive(scoreLightOne, redLight);
				setEmissive(scoreLightTwo, yellowLight);
				setEmissive(scoreLightThree, offColor);
				setEmissive(scoreLightFour, offColor);
				setEmissive(scoreLightFive, offColor);
			}
			else if (values.Score < 0.71f)
			{
				setEmissive(scoreLightOne, redLight);
				setEmissive(scoreLightTwo, yellowLight);
				setEmissive(scoreLightThree, yellowLight);
				setEmissive(scoreLightFour, offColor);
				setEmissive(scoreLightFive, offColor);
			}
			else if (values.Score < 0.86f)
			{
				setEmissive(scoreLightOne, redLight);
				setEmissive(scoreLightTwo, yellowLight);
				setEmissive(scoreLightThree, yellowLight);
				setEmissive(scoreLightFour, greenLight);
				setEmissive(scoreLightFive, offColor);
			}
			else
			{
				setEmissive(scoreLightOne, redLight);
				setEmissive(scoreLightTwo, yellowLight);
				setEmissive(scoreLightThree, yellowLight);
				setEmissive(scoreLightFour, greenLight);
				setEmissive(scoreLightFive, greenLight);
			}
		}

		private void setEmissive(Material m, Color c)
		{
			if (m == null)
				return;

			Color old = m.GetColor("_EmissiveColor");

			Color target = Color.Lerp(old, c, TimeWarp.deltaTime);

			m.SetColor("_EmissiveColor", target);
		}

		protected override void EventsCheck()
		{
			base.EventsCheck();

			Events["DeployExperiment"].active = IsDeployed;
		}

		public override void deployEvent()
		{
			base.deployEvent();
			Fields["scoreString"].guiActive = true;
			if (values != null)
				values.Armed = true;
		}

		public override void retractEvent()
		{
			base.retractEvent();
			Fields["scoreString"].guiActive = false;
			if (values != null)
				values.Armed = false;
		}

		#region Science Setup

		public override void gatherScienceData(bool silent = false)
		{
			if (!canConduct())
			{
				ScreenMessages.PostScreenMessage(failMessage, 5f, ScreenMessageStyle.UPPER_CENTER);
				return;
			}

			if (!IsDeployed)
				deployEvent();

			getScienceData(values.NearbySensorCount <= 0, values.OnAsteroid, silent);
		}

		private void getScienceData(bool sensorOnly, bool asteroid, bool silent)
		{
			ScienceData data = null;

			if (asteroid)
			{
				DMSeismicHammer hammer = vessel.FindPartModulesImplementing<DMSeismicHammer>().FirstOrDefault();

				if (hammer == null)
					data = DMSeismicHandler.makeData(values, values.Score, exp, experimentID, sensorOnly, asteroid);
				else
				{
					hammer.DeployExperiment();
					return;
				}
			}
			else
			{
				DMSeismometerValues v = values.getBestHammer();
				data = DMSeismicHandler.makeData(v, v.Score, exp, experimentID, sensorOnly, asteroid);
			}

			if (data == null)
				return;

			GameEvents.OnExperimentDeployed.Fire(data);

			scienceReports.Add(data);
			Deployed = true;
			if (!silent)
				ReviewData();
		}

		protected override bool canConduct()
		{
			failMessage = "";
			if (Inoperable)
			{
				failMessage = "Experiment is no longer functional; must be reset at a science lab or returned to Kerbin";
				return false;
			}
			else if (Deployed)
			{
				failMessage = experimentFullMessage;
				return false;
			}
			else if (scienceReports.Count > 0)
			{
				failMessage = experimentFullMessage;
				return false;
			}
			else if (vessel.situation != Vessel.Situations.LANDED && vessel.situation != Vessel.Situations.PRELAUNCH && !DMAsteroidScience.AsteroidGrappled)
			{
				failMessage = customFailMessage;
				return false;
			}
			else if (FlightGlobals.ActiveVessel.isEVA)
			{
				if (!ScienceUtil.RequiredUsageExternalAvailable(part.vessel, FlightGlobals.ActiveVessel, (ExperimentUsageReqs)usageReqMaskExternal, exp, ref usageReqMessage))
				{
					failMessage = usageReqMessage;
					return false;
				}
				else
					return true;
			}
			else
				return true;
		}

		#endregion
	}
}
