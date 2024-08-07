using System.Collections.Generic;
using Script.Component;
using Script.Enum;
using Script.Task;
using Script.Utils;
using UnityEngine;

namespace Script.Controller {
    public class ComponentControl : MonoBehaviour {

        //Add comment to a script
        [TextArea(1, 5)]
        public string Notes = "Comment";

        //--------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// KEY - Selected object | VALUE - Data about selected object like all components
        /// Component list for storing data about components for each selected object.
        /// </summary>
        private Dictionary<Transform, ComponentData> _componentList = new();

        [Header("Actual components")]
        public Transform componentPanel;
        public GameObject componentGridPrefab;

        private void Start() {
            ObjectControl.OnObjectRemove += OnObjectRemove;
            ATask.OnPartCreation += CreateGridForObject;
        }

        private void OnDestroy() {
            ObjectControl.OnObjectRemove -= OnObjectRemove;
            ATask.OnPartCreation -= CreateGridForObject;
        }

        private void OnObjectRemove(Transform selectedObject) {
            DisableComponents(selectedObject);
            Destroy(_componentList[selectedObject].ComponentGrid);
            _componentList.Remove(selectedObject);
        }

        public void EnableComponents(Transform selectedObject) {
            if (selectedObject is null) return;
            _componentList[selectedObject].ComponentGrid.SetActive(true);
        }

        public void DisableComponents(Transform selectedObject) {
            if (selectedObject is null) return;
            _componentList[selectedObject].ComponentGrid.SetActive(false);
        }

        private AComponent CreateComponent(GameObject componentGrid, Transform selectedObject,
            KeyValuePair<ComponentType, GameObject> pair) {
            var component = Instantiate(pair.Value, componentGrid.transform.GetChild(0).GetChild(0));
            var componentScript = component.GetComponent<AComponent>();
            componentScript.Initialize(selectedObject, componentGrid);
            return null;
        }

        /// <summary>
        /// Create grid for created object.
        /// </summary>
        public void CreateGridForObject(Transform parentObject, Dictionary<ComponentType, GameObject> components) {
            var componentGrid = Instantiate(componentGridPrefab, componentPanel);
            componentGrid.name = parentObject.name + "_componentGrid";

            List<AComponent> componentList = new();
            var obj = ObjectUtils.GetReference(parentObject.transform);
            foreach (var pair in components) {
                componentList.Add(CreateComponent(componentGrid, obj, pair));
            }

            _componentList.Add(obj,
                new ComponentData(obj.gameObject, componentGrid, componentList));
            componentGrid.SetActive(false);
        }

    }
} //END