using System.Collections.Generic;
using Script.Buttons;
using Script.Enum;
using Script.Manager;
using Script.Static;
using Script.Utils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Action = Script.Enum.Action;

namespace Script.Controller {
    public class ObjectControl : MonoBehaviour {

        //Add comment to a script
        [TextArea(1, 5)]
        public string Notes = "Comment";

        //--------------------------------------------------------------------------------------------------------------------------

        public static UnityAction<Transform> OnObjectSelected;
        public static UnityAction<Transform> OnObjectDeselected;
        public static UnityAction<Transform> OnObjectRemove;

        public ComponentControl componentControl;
        public ControlPanel controlPanel;
        public Canvas canvas;
        public Camera mainCamera;
        public GameObject deleteButton;
        private Transform _selectedObject;
        private float _distance;
        private Vector3 _offset;
        private bool _isHoldingObject;

        [Header("Mouse control")]
        public float holdThreshold = 0.2f; // How long the button must be pressed
        public float moveThreshold = 5f; // How much the mouse has to move
        private float _mouseDownTime;
        private Vector3 _mouseDownPosition;
        private bool _isHoldingMouse;
        private bool _isMovingMouse;

        public LayerMask targetLayer;
        private Transform _selectedAxis;
        public Material selectedMaterial;
        private Material _originalMaterial;

        private void Start() {
            deleteButton.SetActive(false);

            controlPanel.OnActionChange += action => EnableActionGraphic(_selectedObject, action);
        }

        private void OnDestroy() {
            controlPanel.OnActionChange -= action => EnableActionGraphic(_selectedObject, action);
        }

        private void Update() {
            if (Input.GetKeyDown(KeyCode.Escape)) {
                DeselectObject();
            }

            if (Input.GetKeyDown(KeyCode.Delete)) {
                if (_selectedObject is not null) {
                    RemoveObject();
                }
            }

            if (Input.GetMouseButtonDown(0)) {
                _mouseDownPosition = Input.mousePosition;
                _mouseDownTime = Time.time;
                _isHoldingMouse = false;
                _isMovingMouse = false;
                SetDistance();
                SetHoldingObject();
            }

            if (Input.GetMouseButton(0)) {
                if (IsMovingMouse()) {
                    _isMovingMouse = true;
                    switch (controlPanel.GetAction()) {
                        case Action.POSITION: {
                            MoveObject();
                        }
                            break;
                        case Action.ROTATION: {
                            RotateObject();
                        }
                            break;
                    }
                }
                else {
                    if (IsHoldingMouse()) {
                        _isHoldingMouse = true;
                        SelectObject();
                    }
                }
            }
            else {
                SelectAxis();
            }

            if (Input.GetMouseButtonUp(0)) {
                if (!_isMovingMouse && !_isHoldingMouse) {
                    SelectObject();
                }

                _isHoldingMouse = false;
                _isMovingMouse = false;
                _isHoldingObject = false;

                if (_selectedObject is not null)
                    ObjectUtils.GetTorus(_selectedObject).transform.rotation = new Quaternion(0, 0, 0, 0);
            }
        }

        private void SelectObject() {
            var ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit)) {
                if (!hit.collider.CompareTag(Tags.Selectable)) return;

                if (_selectedObject == hit.transform) return;

                if (_selectedObject != hit.transform || _selectedObject is null) {
                    SelectObject(hit.transform);
                }
            }
            else {
                if (IsPointerOverUI(Tags.ComponentPanel) || IsPointerOverUI(Tags.ControlPanel)) return;
                DeselectObject();
            }
        }

        public void SelectObject(Transform selectable) {
            //Old
            Outline(_selectedObject, false);

            if (_selectedObject is not null)
                OnObjectDeselected?.Invoke(_selectedObject);

            componentControl.DisableComponents(_selectedObject);
            ObjectUtils.SetCanvasVisible(_selectedObject);

            SetSelectedObject(selectable);
            deleteButton.SetActive(true);

            //New
            Outline(_selectedObject, true);
            componentControl.EnableComponents(_selectedObject);
            ObjectUtils.SetCanvasVisible(_selectedObject);
            EnableActionGraphic(_selectedObject, controlPanel.GetAction());
        }

        private void DeselectObject() {
            if (_selectedObject is null) return;
            OnObjectDeselected?.Invoke(_selectedObject);
            ObjectUtils.SetCanvasVisible(_selectedObject);
            componentControl.DisableComponents(_selectedObject);
            Outline(_selectedObject, false);
            SetSelectedObject(null);
            deleteButton.SetActive(false);
        }

        private void EnableActionGraphic(Transform selectedObject, Action action) {
            if (selectedObject is null) return;

            switch (action) {
                case Action.POSITION:
                    ObjectUtils.EnableArrows(selectedObject);
                    break;
                case Action.ROTATION:
                    ObjectUtils.EnableTorus(selectedObject);
                    break;
            }
        }

        private void SelectAxis() {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, Mathf.Infinity, targetLayer)) {
                GameObject hitObject = hit.collider.gameObject;

                if (hitObject.CompareTag(Tags.Axis)) {
                    if (_selectedAxis != hitObject) {
                        if (_selectedAxis != null) {
                            _selectedAxis.GetComponent<MeshRenderer>().material = _originalMaterial;
                        }

                        _originalMaterial = hitObject.GetComponent<MeshRenderer>().material;

                        hitObject.GetComponent<MeshRenderer>().material = selectedMaterial;

                        _selectedAxis = hitObject.transform;
                    }
                }
            }
            else {
                if (_selectedAxis != null) {
                    _selectedAxis.GetComponent<MeshRenderer>().material = _originalMaterial;
                    _selectedAxis = null;
                }
            }
        }

        private void MoveObject() {
            if (!_isHoldingObject) return;

            Axis axis = _selectedAxis is null ? Axis.FREE : _selectedAxis.GetComponent<ButtonAxis>().axis;

            var mousePosition = Input.mousePosition;
            mousePosition.z = _distance;
            var worldPos = mainCamera.ScreenToWorldPoint(mousePosition);
            var newPosition = _selectedObject.position;

            switch (axis) {
                case Axis.X: {
                    newPosition.x = worldPos.x + _offset.x;
                }
                    break;
                case Axis.Y: {
                    newPosition.y = worldPos.y + _offset.y;
                }
                    break;
                case Axis.Z: {
                    newPosition.z = worldPos.z + _offset.z;
                }
                    break;
                case Axis.FREE: {
                    var screenPosition = new Vector3(Input.mousePosition.x, Input.mousePosition.y, _distance);
                    var pos = mainCamera.ScreenToWorldPoint(screenPosition);
                    SetPosition(pos + _offset);
                    return;
                }
            }

            SetPosition(newPosition);
        }

        private void RotateObject() {
            if (!_isHoldingObject || _selectedAxis is null) return;
            float mouse = 0f;
            Vector3 rotate = Vector3.zero;
            switch (_selectedAxis.GetComponent<ButtonAxis>().axis) { //TODO fix rotation
                case Axis.X: {
                    mouse = Input.GetAxis("Mouse Y");
                    rotate = Vector3.left;
                }
                    break;
                case Axis.Y: {
                    mouse = Input.GetAxis("Mouse X");
                    rotate = Vector3.up;
                }
                    break;
                case Axis.Z: {
                    mouse = Input.GetAxis("Mouse Y");
                    rotate = Vector3.forward;
                }
                    break;
                case Axis.FREE: {
                    float mouseX = Input.GetAxis("Mouse X") * 2;
                    float mouseY = Input.GetAxis("Mouse Y") * 2;
                    SetRotation(mouseY, -mouseX, 0);
                }
                    break;
            }

            SetRotation(ObjectUtils.GetObjectCenter(_selectedObject), rotate, -mouse * 2f);
        }

        /// <summary>
        /// Set distance from main camera to object.
        /// Set offset from object middle to mouse position.
        /// </summary>
        private void SetDistance() {
            if (_selectedObject is null) return;
            var position = _selectedObject.position;
            var screenPoint = mainCamera.WorldToScreenPoint(position);
            _distance = screenPoint.z;
            _offset = position -
                      mainCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y,
                          _distance));
        }

        private void SetPosition(Vector3 pos) {
            _selectedObject.position = pos;
            ObjectUtils.GetCanvas(_selectedObject).position = ObjectUtils.GetObjectCenter(_selectedObject);
        }

        private void SetRotation(Vector3 point, Vector3 axis, float angle) {
            _selectedObject.RotateAround(point, axis, angle);
            ObjectUtils.GetTorus(_selectedObject).transform.Rotate(axis, angle, Space.World);
        }

        private void SetRotation(float xAngle, float yAngle, float zAngle) {
            _selectedObject.Rotate(xAngle, yAngle, zAngle, Space.World);
        }

        /// <summary>
        /// Checks if the selected object has been hit.
        /// </summary>
        private void SetHoldingObject() {
            var ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit)) {
                if (hit.transform == _selectedObject || _selectedAxis is not null) {
                    _isHoldingObject = true;
                }
                else {
                    _isHoldingObject = false;
                }
            }
        }

        private void SetSelectedObject(Transform selectedObject) {
            _selectedObject = selectedObject;
            OnObjectSelected?.Invoke(selectedObject);
        }

        /// <summary>
        /// Check that the mouse button has been pressed for longer than the threshold.
        /// </summary>
        /// <returns></returns>
        private bool IsHoldingMouse() {
            return !_isHoldingMouse && Time.time - _mouseDownTime >= holdThreshold;
        }

        /// <summary>
        /// Check if the mouse has been moved.
        /// </summary>
        /// <returns></returns>
        private bool IsMovingMouse() {
            return Vector3.Distance(_mouseDownPosition, Input.mousePosition) > moveThreshold;
        }

        private void Outline(Transform selectedObject, bool enabled) {
            if (selectedObject is not null)
                selectedObject.GetComponent<Outline>().enabled = enabled;
        }

        private bool IsPointerOverUI(string tag) {
            PointerEventData eventData = new PointerEventData(EventSystem.current);
            eventData.position = Input.mousePosition;

            List<RaycastResult> results = new List<RaycastResult>();

            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            raycaster.Raycast(eventData, results);

            foreach (RaycastResult result in results) {
                if (result.gameObject.CompareTag(tag)) {
                    return true;
                }
            }

            return false;
        }

        public void RemoveObject() {
            OnObjectRemove?.Invoke(_selectedObject);
            SaveManager.Instance.Remove(_selectedObject);
            Destroy(_selectedObject.parent.gameObject);
            _selectedObject = null;
            deleteButton.SetActive(false);
        }

    }
} //END