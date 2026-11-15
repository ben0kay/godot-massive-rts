@tool
extends Node3D
## Previews an upright command prop on the shared authored surface.

@export var footprint_half_extent := Vector2.ZERO
var _preview_seconds := 0.0
var _snap_queued := false

func _ready() -> void:
	set_process(Engine.is_editor_hint())
	if Engine.is_editor_hint():
		set_notify_transform(true)
		_snap_to_ground.call_deferred()

func _process(delta: float) -> void:
	_preview_seconds += delta
	if _preview_seconds >= 0.25:
		_preview_seconds = 0.0
		_snap_to_ground()

func _notification(what: int) -> void:
	if what == NOTIFICATION_TRANSFORM_CHANGED and Engine.is_editor_hint() and not _snap_queued:
		_snap_queued = true
		_snap_to_ground.call_deferred()

func _snap_to_ground() -> void:
	_snap_queued = false
	if not Engine.is_editor_hint() or not is_inside_tree():
		return
	var center := Vector2(global_position.x, global_position.z)
	var minimum := center - footprint_half_extent
	var maximum := center + footprint_half_extent
	var base_y := global_position.y
	var geometry: Mesh = get("mesh") if is_class("MeshInstance3D") else null
	if geometry != null:
		var bounds: AABB = global_transform * geometry.get_aabb()
		minimum = Vector2(bounds.position.x, bounds.position.z)
		maximum = Vector2(bounds.end.x, bounds.end.z)
		base_y = bounds.position.y

	var terrain: Node = null
	var parent := get_parent()
	while parent != null:
		terrain = parent.get_node_or_null("Terrain")
		if terrain != null and terrain.has_method("SampleFootprint"):
			break
		terrain = null
		parent = parent.get_parent()
	var ground_y := 0.0 if terrain == null else float(terrain.call("SampleFootprint", minimum, maximum))
	if not is_equal_approx(base_y, ground_y):
		var grounded := global_position
		grounded.y += ground_y - base_y
		global_position = grounded
