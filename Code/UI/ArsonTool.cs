using System.Collections.Generic;
using ColossalFramework;
using ColossalFramework.UI;
using UnityEngine;
using UnifiedUI.Helpers;


namespace Arson
{
    /// <summary>
    /// The arson tool.
    /// </summary>
    public class ArsonTool : DefaultTool
    {
        // Cursor textures.
        private CursorInfo arsonCursor;

        // Box-selection drag state (screen coordinates, with origin at bottom-left).
        private bool _selecting = false;
        private Vector2 _selectionStart;
        private Vector2 _selectionEnd;

        // 1x1 white texture, stretched to draw the selection rectangle.
        private Texture2D selectionTexture;

        // Drags shorter than this (in pixels) are treated as single clicks.
        private const float ClickThreshold = 8f;

        // Selection rectangle colours.
        private static readonly Color SelectionFillColor = new Color(1f, 0.35f, 0f, 0.25f);
        private static readonly Color SelectionBorderColor = new Color(1f, 0.5f, 0f, 0.9f);


        /// <summary>
        /// Instance reference.
        /// </summary>
        public static ArsonTool Instance => ToolsModifierControl.toolController?.gameObject?.GetComponent<ArsonTool>();

        /// <summary>
        /// Returns true if the zoning tool is currently active, false otherwise.
        /// </summary>
        public static bool IsActiveTool => Instance != null && ToolsModifierControl.toolController.CurrentTool == Instance;


        /// <summary>
        /// Initialise the tool.
        /// Called by unity when the tool is created.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            // Load cursors.
            arsonCursor = TextureUtils.LoadCursor("ArsonUUI.png");
            arsonCursor.m_hotspot = new Vector2(16f, 28f);
            m_cursor = arsonCursor;

            // Create the selection rectangle texture.
            selectionTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            selectionTexture.SetPixel(0, 0, Color.white);
            selectionTexture.Apply();

            // Create new UUI button.
            UIComponent uuiButton = UUIHelpers.RegisterToolButton(
                name: nameof(ArsonTool),
                groupName: null, // default group
                tooltip: Translations.Translate("ARS_NAM"),
                tool: this,
                icon: UUIHelpers.LoadTexture(UUIHelpers.GetFullPath<ArsonMod>("Resources", "ArsonUUI.png")),
                hotkeys: new UUIHotKeys { ActivationKey = ModSettings.ToolSavedKey });
        }


        // Ignore nodes, citizens, disasters, districts, transport lines, and vehicles.
        public override NetNode.Flags GetNodeIgnoreFlags() => NetNode.Flags.All;
        public override CitizenInstance.Flags GetCitizenIgnoreFlags() => CitizenInstance.Flags.All;
        public override DisasterData.Flags GetDisasterIgnoreFlags() => DisasterData.Flags.All;
        public override District.Flags GetDistrictIgnoreFlags() => District.Flags.All;
        public override TransportLine.Flags GetTransportIgnoreFlags() => TransportLine.Flags.None;
        public override VehicleParked.Flags GetParkedVehicleIgnoreFlags() => VehicleParked.Flags.All;
        public override TreeInstance.Flags GetTreeIgnoreFlags() => TreeInstance.Flags.None;
        public override PropInstance.Flags GetPropIgnoreFlags() => PropInstance.Flags.All;
        public override Vehicle.Flags GetVehicleIgnoreFlags() => Vehicle.Flags.LeftHandDrive | Vehicle.Flags.Created | Vehicle.Flags.Deleted | Vehicle.Flags.Spawned | Vehicle.Flags.Inverted | Vehicle.Flags.TransferToTarget | Vehicle.Flags.TransferToSource | Vehicle.Flags.Emergency1 | Vehicle.Flags.Emergency2 | Vehicle.Flags.WaitingPath | Vehicle.Flags.Stopped | Vehicle.Flags.Leaving | Vehicle.Flags.Arriving | Vehicle.Flags.Reversed | Vehicle.Flags.TakingOff | Vehicle.Flags.Flying | Vehicle.Flags.Landing | Vehicle.Flags.WaitingSpace | Vehicle.Flags.WaitingCargo | Vehicle.Flags.GoingBack | Vehicle.Flags.WaitingTarget | Vehicle.Flags.Importing | Vehicle.Flags.Exporting | Vehicle.Flags.Parking | Vehicle.Flags.CustomName | Vehicle.Flags.OnGravel | Vehicle.Flags.WaitingLoading | Vehicle.Flags.Congestion | Vehicle.Flags.DummyTraffic | Vehicle.Flags.Underground | Vehicle.Flags.Transition | Vehicle.Flags.InsideBuilding;


        // Select all buildings.
        public override Building.Flags GetBuildingIgnoreFlags() => Building.Flags.None;


        /// <summary>
        /// Called by the game.  Sets which network segments are ignored by the tool (always returns all, i.e. no segments are selectable by the tool).
        /// </summary>
        /// <param name="nameOnly">Always set to false</param>
        /// <returns>NetSegment.Flags.All</returns>
        public override NetSegment.Flags GetSegmentIgnoreFlags(out bool nameOnly)
        {
            nameOnly = false;
            return NetSegment.Flags.All;
        }


        /// <summary>
        /// Called by the game every simulation step.
        /// Performs raycasting to select hovered instance.
        /// </summary>
        public override void SimulationStep()
        {
            UpdateHoverInstance();
        }


        /// <summary>
        /// Called by the game every frame while this tool is active.
        /// Tracks box-selection drag input.
        /// </summary>
        protected override void OnToolUpdate()
        {
            base.OnToolUpdate();

            // Input is tracked here (not in SimulationStep) so that dragging also works when the simulation isn't advancing (e.g. while the game is paused) and at any rendering frame rate.
            UpdateSelectionDrag();
        }


        /// <summary>
        /// Performs raycasting to determine the currently hovered instance.
        /// </summary>
        private void UpdateHoverInstance()
        {
            // Get base mouse ray.
            Ray mouseRay = m_mouseRay;

            // Get raycast input.
            RaycastInput input = new RaycastInput(mouseRay, m_mouseRayLength)
            {
                m_rayRight = m_rayRight,
                m_netService = GetService(),
                m_buildingService = GetService(),
                m_propService = GetService(),
                m_treeService = GetService(),
                m_districtNameOnly = Singleton<InfoManager>.instance.CurrentMode != InfoManager.InfoMode.Districts,
                m_ignoreTerrain = GetTerrainIgnore(),
                m_ignoreNodeFlags = GetNodeIgnoreFlags(),
                m_ignoreSegmentFlags = GetSegmentIgnoreFlags(out input.m_segmentNameOnly),
                m_ignoreBuildingFlags = GetBuildingIgnoreFlags(),
                m_ignoreTreeFlags = GetTreeIgnoreFlags(),
                m_ignorePropFlags = GetPropIgnoreFlags(),
                m_ignoreVehicleFlags = GetVehicleIgnoreFlags(),
                m_ignoreParkedVehicleFlags = GetParkedVehicleIgnoreFlags(),
                m_ignoreCitizenFlags = GetCitizenIgnoreFlags(),
                m_ignoreTransportFlags = GetTransportIgnoreFlags(),
                m_ignoreDistrictFlags = GetDistrictIgnoreFlags(),
                m_ignoreParkFlags = GetParkIgnoreFlags(),
                m_ignoreDisasterFlags = GetDisasterIgnoreFlags(),
                m_transportTypes = GetTransportTypes()
            };

            ToolErrors errors = ToolErrors.None;
            RaycastOutput output;

            // Is the base mouse ray valid?
            if (m_mouseRayValid)
            {
                // Yes - raycast.
                if (RayCast(input, out output))
                {
                    // Create new hover instance.
                    InstanceID hoverInstance = InstanceID.Empty;

                    // Set base tool accurate position.
                    m_accuratePosition = output.m_hitPos;

                    // Check for building hits.
                    if (output.m_building != 0)
                    {
                        // Building has eligible transfers - set hover, and set cursor to light/
                        hoverInstance.Building = (ushort)output.m_building;
                    }
                    else if (output.m_treeInstance != 0)
                    {
                        // Tree instance (for starting forest fires).
                        hoverInstance.Tree = output.m_treeInstance;
                    }

                    // Update tool hover instance.
                    m_hoverInstance = hoverInstance;
                }
                else
                {
                    // Raycast failed.
                    errors = ToolErrors.RaycastFailed;
                }
            }
            else
            {
                // No valid mouse ray.
                output = default;
                errors = ToolErrors.RaycastFailed;
            }

            // Set mouse position and record errors.
            m_mousePosition = output.m_hitPos;
            m_selectErrors = errors;
        }


        /// <summary>
        /// Tracks box-selection drag input (mouse down, drag, release).
        /// </summary>
        private void UpdateSelectionDrag()
        {
            // Start a new selection on left mouse down outside the UI.
            if (Input.GetMouseButtonDown(0))
            {
                if (!m_toolController.IsInsideUI)
                {
                    _selecting = true;
                    _selectionStart = Input.mousePosition;
                    _selectionEnd = _selectionStart;
                }

                return;
            }

            // No drag in progress - nothing more to do.
            if (!_selecting)
            {
                return;
            }

            if (Input.GetMouseButton(0))
            {
                // Track the ongoing drag.
                _selectionEnd = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                // Mouse button released - apply the selection.
                _selecting = false;
                _selectionEnd = Input.mousePosition;
                ApplySelection();
            }
            else
            {
                // Stale drag (e.g. the mouse was released while the tool was inactive) - discard it.
                _selecting = false;
            }
        }


        /// <summary>
        /// Toggles the current tool to/from the ArsonTool.
        /// </summary>
        internal static void ToggleTool()
        {
            // Activate ArsonTool tool if it isn't already; if already active, deactivate it by selecting the default tool instead.
            if (!IsActiveTool)
            {
                // Activate tool.
                ToolsModifierControl.toolController.CurrentTool = Instance;
            }
            else
            {
                // Activate default tool.
                ToolsModifierControl.SetTool<DefaultTool>();
            }
        }

        /// <summary>
        /// Unity late update handling.
        /// Called by game every late update.
        /// </summary>
        protected override void OnToolLateUpdate()
        {
            base.OnToolLateUpdate();

            // Force the info mode to none.
            ToolBase.ForceInfoMode(InfoManager.InfoMode.None, InfoManager.SubInfoMode.None);
        }


        /// <summary>
        /// Called when the tool is deactivated, to cancel any in-progress drag.
        /// </summary>
        protected override void OnDisable()
        {
            base.OnDisable();

            _selecting = false;
        }


        /// <summary>
        /// Tool GUI event processing.
        /// Called by game every GUI update.
        /// </summary>
        /// <param name="e">Event</param>
        protected override void OnToolGUI(Event e)
        {
            // Consume left clicks outside the UI.
            if (e.type == EventType.MouseDown && e.button == 0 && !m_toolController.IsInsideUI)
            {
                UIInput.MouseUsed();
            }

            // Draw the selection rectangle while dragging.
            if (_selecting && e.type == EventType.Repaint)
            {
                DrawSelectionRect();
            }
        }


        /// <summary>
        /// Sets fire to the hovered building or tree.
        /// </summary>
        public void HandleClick()
        {
            // Hovered instances (zero if none).
            ushort building = m_hoverInstance.Building;
            uint treeId = m_hoverInstance.Tree;

            // Nothing to do if nothing is hovered.
            if (building == 0 && treeId == 0)
            {
                return;
            }

            // Start fire(s) via simulation thread.
            Singleton<SimulationManager>.instance.AddAction(delegate
            {
                if (building != 0)
                {
                    StartBuildingFire(building);
                }

                if (treeId != 0)
                {
                    StartTreeFire(treeId);
                }
            });
        }


        /// <summary>
        /// Applies a completed selection drag: small drags act as single clicks, larger drags set fire to every building and tree within the dragged rectangle.
        /// </summary>
        private void ApplySelection()
        {
            // Treat short drags as single clicks on the hovered instance.
            Vector2 drag = _selectionEnd - _selectionStart;
            if (Mathf.Max(Mathf.Abs(drag.x), Mathf.Abs(drag.y)) < ClickThreshold)
            {
                // Refresh the hover instance - the simulation-driven hover update may be stale (e.g. while paused).
                UpdateHoverInstance();
                HandleClick();
                return;
            }

            // Selection rectangle in screen coordinates (origin at bottom-left, as used by WorldToScreenPoint).
            float minX = Mathf.Min(_selectionStart.x, _selectionEnd.x);
            float maxX = Mathf.Max(_selectionStart.x, _selectionEnd.x);
            float minY = Mathf.Min(_selectionStart.y, _selectionEnd.y);
            float maxY = Mathf.Max(_selectionStart.y, _selectionEnd.y);

            // Gather all buildings and trees inside the selection.
            List<ushort> buildings = new List<ushort>();
            List<uint> trees = new List<uint>();

            BuildingManager buildingManager = Singleton<BuildingManager>.instance;
            for (uint i = 1; i < buildingManager.m_buildings.m_size; ++i)
            {
                ref Building building = ref buildingManager.m_buildings.m_buffer[i];
                if ((building.m_flags & Building.Flags.Created) == 0 || (building.m_flags & Building.Flags.Deleted) != 0)
                {
                    continue;
                }

                if (IsWithinSelection(building.m_position, minX, maxX, minY, maxY))
                {
                    buildings.Add((ushort)i);
                }
            }

            TreeManager treeManager = Singleton<TreeManager>.instance;
            for (uint i = 1; i < treeManager.m_trees.m_size; ++i)
            {
                ref TreeInstance tree = ref treeManager.m_trees.m_buffer[i];
                if ((tree.m_flags & (ushort)TreeInstance.Flags.Created) == 0)
                {
                    continue;
                }

                if (IsWithinSelection(tree.Position, minX, maxX, minY, maxY))
                {
                    trees.Add(i);
                }
            }

            // Nothing selected - nothing to do.
            if (buildings.Count == 0 && trees.Count == 0)
            {
                return;
            }

            // Start the fires via simulation thread.
            Singleton<SimulationManager>.instance.AddAction(delegate
            {
                foreach (ushort buildingId in buildings)
                {
                    StartBuildingFire(buildingId);
                }

                foreach (uint treeId in trees)
                {
                    StartTreeFire(treeId);
                }
            });
        }


        /// <summary>
        /// Checks whether the given world position projects inside the given screen rectangle (origin at bottom-left).
        /// </summary>
        /// <param name="worldPosition">World position to check</param>
        /// <param name="minX">Left edge of the selection</param>
        /// <param name="maxX">Right edge of the selection</param>
        /// <param name="minY">Bottom edge of the selection</param>
        /// <param name="maxY">Top edge of the selection</param>
        /// <returns>True if the position is within the selection, false otherwise</returns>
        private static bool IsWithinSelection(Vector3 worldPosition, float minX, float maxX, float minY, float maxY)
        {
            Vector3 screenPosition = Camera.main.WorldToScreenPoint(worldPosition);

            return screenPosition.z > 0f
                && screenPosition.x >= minX && screenPosition.x <= maxX
                && screenPosition.y >= minY && screenPosition.y <= maxY;
        }


        /// <summary>
        /// Starts a fire in the specified building.
        /// </summary>
        /// <param name="buildingId">Building ID</param>
        private static void StartBuildingFire(ushort buildingId)
        {
            BuildingManager buildingManager = Singleton<BuildingManager>.instance;
            ref Building building = ref buildingManager.m_buildings.m_buffer[buildingId];

            // Ignore buildings that are no longer valid.
            if ((building.m_flags & Building.Flags.Created) == 0 || (building.m_flags & Building.Flags.Deleted) != 0 || building.Info == null)
            {
                return;
            }

            InstanceID instance = default(InstanceID);
            instance.Building = buildingId;
            InstanceManager.Group group = Singleton<InstanceManager>.instance.GetGroup(instance);
            building.Info.m_buildingAI.BurnBuilding(buildingId, ref building, group, false);
        }


        /// <summary>
        /// Starts a fire in the specified tree.
        /// </summary>
        /// <param name="treeId">Tree ID</param>
        private static void StartTreeFire(uint treeId)
        {
            TreeManager treeManager = Singleton<TreeManager>.instance;
            ref TreeInstance tree = ref treeManager.m_trees.m_buffer[treeId];

            // Ignore trees that are no longer valid.
            if ((tree.m_flags & (ushort)TreeInstance.Flags.Created) == 0)
            {
                return;
            }

            Vector3 position = tree.Position;

            // Try and find or create a forest fire disaster group for this fire.
            ushort disasterId = DisasterManager.instance.FindDisaster<ForestFireAI>(position);
            if (disasterId == 0)
            {
                DisasterManager disasterManager = Singleton<DisasterManager>.instance;
                DisasterInfo disasterInfo = DisasterManager.FindDisasterInfo<ForestFireAI>();
                if (disasterInfo is object && disasterManager.CreateDisaster(out disasterId, disasterInfo))
                {
                    ref DisasterData data = ref disasterManager.m_disasters.m_buffer[disasterId];
                    data.m_targetPosition = position;
                    data.m_intensity = (byte)Singleton<SimulationManager>.instance.m_randomizer.Int32(10, 100);
                    data.m_angle = 1.0f;
                    data.m_flags |= DisasterData.Flags.SelfTrigger;
                    disasterInfo.m_disasterAI.StartNow(disasterId, ref data);
                }
            }

            // Attempt to set fire to this tree if we've got a valid disaster ID.
            if (disasterId != 0)
            {
                InstanceID instance = default(InstanceID);
                instance.Disaster = disasterId;
                InstanceManager.Group group = Singleton<InstanceManager>.instance.GetGroup(instance);
                treeManager.BurnTree(treeId, group, Singleton<SimulationManager>.instance.m_randomizer.Int32(140, 255));
            }
        }


        /// <summary>
        /// Draws the selection rectangle.
        /// </summary>
        private void DrawSelectionRect()
        {
            // Convert screen coordinates (origin at bottom-left) to GUI coordinates (origin at top-left).
            float left = Mathf.Min(_selectionStart.x, _selectionEnd.x);
            float bottom = Mathf.Min(_selectionStart.y, _selectionEnd.y);
            float width = Mathf.Abs(_selectionEnd.x - _selectionStart.x);
            float height = Mathf.Abs(_selectionEnd.y - _selectionStart.y);
            float top = Screen.height - bottom - height;

            // Fill.
            GUI.color = SelectionFillColor;
            GUI.DrawTexture(new Rect(left, top, width, height), selectionTexture);

            // Border.
            const float borderWidth = 2f;
            GUI.color = SelectionBorderColor;
            GUI.DrawTexture(new Rect(left, top, width, borderWidth), selectionTexture);
            GUI.DrawTexture(new Rect(left, top + height - borderWidth, width, borderWidth), selectionTexture);
            GUI.DrawTexture(new Rect(left, top, borderWidth, height), selectionTexture);
            GUI.DrawTexture(new Rect(left + width - borderWidth, top, borderWidth, height), selectionTexture);

            GUI.color = Color.white;
        }
    }
}
