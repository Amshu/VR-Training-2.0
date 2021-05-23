﻿using System.Linq;
using UnityEngine;

using BNG;

namespace HYDAC.Scripts.VRT {
    /// <summary>
    /// Show a ring helper on an object. This class makes sure the ring helper is added to the object
    /// </summary>
    public class VRT_GrabbableRingHelper : GrabbableEvents {

        public bool RingOnValidPickup = true;
        public bool RingOnValidRemotePickup = true;

        public float RingHelperScale = 0.2f;

        void Start() {
            // Set up Ring
            if (RingOnValidPickup) {
                VRT_RingHelper rh = GetComponentInChildren<VRT_RingHelper>();
                if (rh == null) {
                    GameObject go = Instantiate(Resources.Load("RingHelper", typeof(GameObject))) as GameObject;
                    go.transform.SetParent(this.transform, false);
                    go.transform.name = "Ring Helper";
                    go.transform.localPosition = grab.GrabPositionOffset;

                    // Use GrabPoint if set
                    if(grab.GrabPoints != null && grab.GrabPoints.Count > 0) {
                        go.transform.localPosition = grab.GrabPoints.FirstOrDefault().localPosition;
                    }
                           
                    RectTransform rt = go.GetComponent<RectTransform>();
                    rt.localScale = new Vector3(RingHelperScale, RingHelperScale, RingHelperScale);
                }
            }
        }
    }
}