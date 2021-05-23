using System.Collections;
using UnityEngine;

using BNG;
using UnityEngine.Serialization;

namespace HYDAC.Scripts.VRT {

    /// <summary>
    /// Constrain a magazine when it enters this area. Attaches the magazine in place if close enough.
    /// </summary>
    public class VRT_FilterSlide : MonoBehaviour {

        /// <summary>
        /// Clip transform name must contain this to be considered valid
        /// </summary>
        public string AcceptableMagazineName = "Filter";

        public float ClipSnapDistance = 0.075f;
        public float ClipUnsnapDistance = 0.15f;

        /// <summary>
        ///  How much force to apply to the inserted magazine if it is forcefully ejected
        /// </summary>
        public float EjectForce = 1f;

        [FormerlySerializedAs("HeldMagazine")] public Grabbable HeldFilter = null;
        Collider HeldCollider = null;

        [FormerlySerializedAs("MagazineDistance")] public float FilterDistance = 0f;

        bool filterInPlace = false;

        // Lock in place for physics
        bool lockedInPlace = false;

        public AudioClip ClipAttachSound;
        public AudioClip ClipDetachSound;

        GrabberArea grabClipArea;

        float lastEjectTime;

        void Awake() {
            grabClipArea = GetComponentInChildren<GrabberArea>();

            // Check to see if we started with a loaded filter
            if(HeldFilter != null) {
                AttachGrabbableMagazine(HeldFilter, HeldFilter.GetComponent<Collider>());
            }
        }        

        void LateUpdate() {

            // Are we trying to grab the clip from the weapon
            CheckGrabClipInput();

            // There is a magazine inside the slide. Position it properly
            if(HeldFilter != null) {
               
                HeldFilter.transform.parent = transform;

                // Lock in place immediately
                if (lockedInPlace) {
                    HeldFilter.transform.localPosition = Vector3.zero;
                    HeldFilter.transform.localEulerAngles = Vector3.zero;
                    return;
                }

                Vector3 localPos = HeldFilter.transform.localPosition;

                // Make sure magazine is aligned with MagazineSlide
                HeldFilter.transform.localEulerAngles = Vector3.zero;

                // Only allow Y translation. Don't allow to go up and through clip area
                float localY = localPos.y;
                if(localY > 0) {
                    localY = 0;
                }

                moveMagazine(new Vector3(0, localY, 0));

                FilterDistance = Vector3.Distance(transform.position, HeldFilter.transform.position);
               
                bool clipRecentlyGrabbed = Time.time - HeldFilter.LastGrabTime < 1f;

                // Snap Magazine In Place
                if (FilterDistance < ClipSnapDistance) {

                    // Snap in place
                    if(!filterInPlace && !recentlyEjected() && !clipRecentlyGrabbed) {
                        AttachFilter();
                    }

                    // Make sure magazine stays in place if not being grabbed
                    if(!HeldFilter.BeingHeld) {
                        moveMagazine(Vector3.zero);
                    }
                }
                // Stop aligning clip with slide if we exceed this distance
                else if(FilterDistance >= ClipUnsnapDistance && !recentlyEjected()) {
                    DetachFilter();
                }
            }
        }

        bool recentlyEjected() {
            return Time.time - lastEjectTime < 0.1f;
        }

        void moveMagazine(Vector3 localPosition) {
            HeldFilter.transform.localPosition = localPosition;
        }

        public void CheckGrabClipInput() {

            // No need to check for grabbing a clip out if none exists
            if(HeldFilter == null || grabClipArea == null) {
                return;
            }

            Grabber nearestGrabber = grabClipArea.GetOpenGrabber();
            if (grabClipArea != null && nearestGrabber != null) 
            {
                if (nearestGrabber.HandSide == ControllerHand.Left && InputBridge.Instance.LeftGripDown) {
                    // grab clip
                    OnGrabClipArea(nearestGrabber);
                }
                else if (nearestGrabber.HandSide == ControllerHand.Right && InputBridge.Instance.RightGripDown) {
                    OnGrabClipArea(nearestGrabber);
                }
            }
        }

        void AttachFilter()
        {
            // Drop Item
            var grabber = HeldFilter.GetPrimaryGrabber();
            HeldFilter.DropItem(grabber, false, false);

            // Play Sound
            VRUtils.Instance.PlaySpatialClipAt(ClipAttachSound, transform.position, 1f);

            // Move to desired location before locking in place
            moveMagazine(Vector3.zero);

            // Add fixed joint to make sure physics work properly
            if (transform.parent != null)
            {
                Rigidbody parentRB = transform.parent.GetComponent<Rigidbody>();
                if (parentRB)
                {
                    FixedJoint fj = HeldFilter.gameObject.AddComponent<FixedJoint>();
                    fj.autoConfigureConnectedAnchor = true;
                    fj.axis = new Vector3(0, 1, 0);
                    fj.connectedBody = parentRB;
                }
            }

            // Don't let anything try to grab the magazine while it's within the weapon
            // We will use a grabbable proxy to grab the clip back out instead
            HeldFilter.enabled = false;

            lockedInPlace = true;
            filterInPlace = true;
        }

        /// <summary>
        /// Detach Magazine from it's parent. Removes joint, re-enables collider, and calls events
        /// </summary>
        /// <returns>Returns the magazine that was ejected or null if no magazine was attached</returns>
        Grabbable DetachFilter() {

            if(HeldFilter == null) {
                return null;
            }

            VRUtils.Instance.PlaySpatialClipAt(ClipDetachSound, transform.position, 1f, 0.9f);
            
            HeldFilter.transform.parent = null;

            // Remove fixed joint
            if (transform.parent != null) {
                Rigidbody parentRB = transform.parent.GetComponent<Rigidbody>();
                if (parentRB) {
                    FixedJoint fj = HeldFilter.gameObject.GetComponent<FixedJoint>();
                    if (fj) {
                        fj.connectedBody = null;
                        Destroy(fj);
                    }
                }
            }

            // Reset Collider
            if (HeldCollider != null) {
                HeldCollider.enabled = true;
                HeldCollider = null;
            }

            // Can be grabbed again
            HeldFilter.enabled = true;
            filterInPlace = false;
            lockedInPlace = false;
            lastEjectTime = Time.time;

            var returnGrab = HeldFilter;
            HeldFilter = null;

            return returnGrab;
        }

        public void EjectMagazine() {
            Grabbable ejectedMag = DetachFilter();
            lastEjectTime = Time.time;

            StartCoroutine(EjectMagRoutine(ejectedMag));
        }

        IEnumerator EjectMagRoutine(Grabbable ejectedMag) {

            if (ejectedMag != null && ejectedMag.GetComponent<Rigidbody>() != null) {

                Rigidbody ejectRigid = ejectedMag.GetComponent<Rigidbody>();

                // Wait before ejecting

                // Move clip down before we eject it
                ejectedMag.transform.parent = transform;

                if(ejectedMag.transform.localPosition.y > -ClipSnapDistance) {
                    ejectedMag.transform.localPosition = new Vector3(0, -0.1f, 0);
                }

                // Eject with physics force
                ejectedMag.transform.parent = null;
                ejectRigid.AddForce(-ejectedMag.transform.up * EjectForce, ForceMode.VelocityChange);

                yield return new WaitForFixedUpdate();
                ejectedMag.transform.parent = null;

            }

            yield return null;
        }

        // Pull out magazine from clip area
        public void OnGrabClipArea(Grabber grabbedBy)
        {
            if (HeldFilter != null)
            {
                // Store reference so we can eject the clip first
                Grabbable temp = HeldFilter;

                // Make sure the magazine can be gripped
                HeldFilter.enabled = true;

                // Eject clip into hand
                DetachFilter();

                // Now transfer grab to the grabber
                temp.enabled = true;

                grabbedBy.GrabGrabbable(temp);
            }
        }

        public virtual void AttachGrabbableMagazine(Grabbable slideObj, Collider slideObjCollider) {
            HeldFilter = slideObj;
            HeldFilter.transform.parent = transform;

            HeldCollider = slideObjCollider;

            // Disable the collider while we're sliding it in to the weapon
            if (HeldCollider != null) {
                HeldCollider.enabled = false;
            }
        }

        void OnTriggerEnter(Collider other) {
            Grabbable grab = other.GetComponent<Grabbable>();
            if (HeldFilter == null && grab != null && grab.transform.name.Contains(AcceptableMagazineName)) {
                AttachGrabbableMagazine(grab, other);
            }
        }
    }
}
