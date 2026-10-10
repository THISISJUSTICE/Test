using System.Collections.Generic;
using UnityEngine;

namespace Commar.CubicLand.Cube
{
    internal sealed class GolemGroundSupportTracker
    {
        private struct SupportContact
        {
            public Collider collider;
            public bool isValid;

            public SupportContact(Collider collider)
            {
                this.collider = collider;
                isValid = false;
            }
        }

        private const float MINIMUM_GROUND_NORMAL_DOT = 0.5f;

        private readonly IGolemObject _golemObject;
        private readonly Component _context;

        private readonly HashSet<Collider> _supportColliders = new HashSet<Collider>();
        private readonly List<SupportContact> _supportContacts = new List<SupportContact>();
        private readonly List<Collider> _invalidSupportColliders = new List<Collider>();

        private Collider _groundedCollider;
        private bool _isGrounded;

        public bool IsGrounded
        {
            get
            {
                ValidateSupports();
                return _isGrounded;
            }
        }

        public Collider GroundedCollider
        {
            get
            {
                ValidateSupports();
                return _groundedCollider;
            }
        }

        public Rigidbody GroundedRigidbody
        {
            get
            {
                ValidateSupports();
                return _groundedCollider != null ? _groundedCollider.attachedRigidbody : null;
            }
        }

        internal GolemGroundSupportTracker(IGolemObject golemObject, Component context)
        {
            _golemObject = golemObject;
            _context = context;
        }

        public bool UpdateCollision(Collision collision)
        {
            if (collision == null)
            {
                Debug.LogWarning($"{nameof(GolemGroundSupportTracker)} received a null collision.", _context);
                return false;
            }

            int contactCount = collision.contactCount;
            if (contactCount <= 0)
            {
                Debug.LogWarning($"{nameof(GolemGroundSupportTracker)} received a collision without contact points.", _context);
                return false;
            }

            Rigidbody rigidbody = _golemObject?.Rigidbody;
            if (rigidbody == null)
            {
                Debug.LogError($"{nameof(GolemGroundSupportTracker)} cannot process a collision without a Rigidbody.", _context);
                return false;
            }

            bool wasGrounded = _isGrounded;
            ContactPoint bestContact = default;
            float bestGroundNormalDot = float.NegativeInfinity;

            _supportContacts.Clear();

            for (int i = 0; i < contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                Collider thisCollider = contact.thisCollider;
                Collider otherCollider = contact.otherCollider;

                if (thisCollider == null || otherCollider == null)
                {
                    Debug.LogWarning($"{nameof(GolemGroundSupportTracker)} found a contact with a missing collider.", _context);
                    continue;
                }

                if (thisCollider.attachedRigidbody != rigidbody)
                {
                    Debug.LogWarning($"{nameof(GolemGroundSupportTracker)} received a contact owned by another Rigidbody.", _context);
                    continue;
                }

                if (otherCollider.attachedRigidbody == rigidbody || otherCollider.transform.IsChildOf(rigidbody.transform))
                {
                    Debug.LogWarning($"{nameof(GolemGroundSupportTracker)} received a self-contact.", _context);
                    continue;
                }

                int supportContactIndex = FindSupportContact(otherCollider);
                if (supportContactIndex < 0)
                {
                    supportContactIndex = _supportContacts.Count;
                    _supportContacts.Add(new SupportContact(otherCollider));
                }

                float groundNormalDot = Vector3.Dot(contact.normal, Vector3.up);
                if (groundNormalDot < MINIMUM_GROUND_NORMAL_DOT)
                    continue;

                SupportContact supportContact = _supportContacts[supportContactIndex];
                supportContact.isValid = true;
                _supportContacts[supportContactIndex] = supportContact;

                if (groundNormalDot > bestGroundNormalDot)
                {
                    bestGroundNormalDot = groundNormalDot;
                    bestContact = contact;
                }
            }

            for (int i = 0; i < _supportContacts.Count; i++)
            {
                SupportContact supportContact = _supportContacts[i];
                if (supportContact.isValid)
                    _supportColliders.Add(supportContact.collider);
                else
                    _supportColliders.Remove(supportContact.collider);
            }

            if (_supportColliders.Count <= 0)
            {
                Clear();
                return false;
            }

            _isGrounded = true;
            if (!wasGrounded && bestGroundNormalDot > float.NegativeInfinity)
                _groundedCollider = bestContact.otherCollider;
            else if (_groundedCollider == null || !_supportColliders.Contains(_groundedCollider))
                SelectGroundedCollider();

            return !wasGrounded;
        }

        public void RemoveCollision(Collision collision)
        {
            if (collision == null)
            {
                Debug.LogWarning($"{nameof(GolemGroundSupportTracker)} received a null collision exit.", _context);
                return;
            }

            Collider exitedCollider = collision.collider;
            if (exitedCollider == null)
            {
                Debug.LogWarning($"{nameof(GolemGroundSupportTracker)} received a collision exit without a collider.", _context);
                ValidateSupports();
                return;
            }

            if (!_supportColliders.Remove(exitedCollider))
                return;

            if (_supportColliders.Count <= 0)
            {
                Clear();
                return;
            }

            if (_groundedCollider == exitedCollider)
                SelectGroundedCollider();
        }

        public void Clear()
        {
            _supportColliders.Clear();
            _supportContacts.Clear();
            _invalidSupportColliders.Clear();
            _groundedCollider = null;
            _isGrounded = false;
        }

        private int FindSupportContact(Collider collider)
        {
            for (int i = 0; i < _supportContacts.Count; i++)
            {
                if (_supportContacts[i].collider == collider)
                    return i;
            }

            return -1;
        }

        private void ValidateSupports()
        {
            if (!_isGrounded)
                return;

            _invalidSupportColliders.Clear();
            foreach (Collider supportCollider in _supportColliders)
            {
                if (supportCollider == null || !supportCollider.enabled || !supportCollider.gameObject.activeInHierarchy)
                    _invalidSupportColliders.Add(supportCollider);
            }

            for (int i = 0; i < _invalidSupportColliders.Count; i++)
                _supportColliders.Remove(_invalidSupportColliders[i]);

            if (_supportColliders.Count <= 0)
            {
                Debug.LogWarning($"{nameof(GolemGroundSupportTracker)} lost all support colliders without receiving a collision exit.", _context);
                Clear();
                return;
            }

            if (_groundedCollider == null || !_supportColliders.Contains(_groundedCollider))
                SelectGroundedCollider();
        }

        private void SelectGroundedCollider()
        {
            _groundedCollider = null;
            foreach (Collider supportCollider in _supportColliders)
            {
                if (supportCollider == null)
                    continue;

                _groundedCollider = supportCollider;
                break;
            }

            _isGrounded = _groundedCollider != null;
        }
    }
}