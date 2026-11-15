@tool
extends MeshInstance3D
## Keeps a command wall's base on the flat gameplay plane while authoring.

var _snap_queued := false

func _ready() -> void:
	_snap_to_ground()
	if Engine.is_editor_hint():
		set_notify_transform(true)

func _notification(what: int) -> void:
	if what == NOTIFICATION_TRANSFORM_CHANGED and Engine.is_editor_hint() and not _snap_queued:
		_snap_queued = true
		_snap_to_ground.call_deferred()

func _snap_to_ground() -> void:
	_snap_queued = false
	if not is_inside_tree() or mesh == null:
		return
	var bounds := global_transform * mesh.get_aabb()
	if not is_zero_approx(bounds.position.y):
		var position_on_ground := global_position
		position_on_ground.y -= bounds.position.y
		global_position = position_on_ground
