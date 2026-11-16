@tool
extends "res://WORLD/TERRAIN/AUTHORING/CommandSurfaceSnap.gd"
## Saves the wall's edited mesh dimensions as a per-node scene override.

var _editable_mesh: BoxMesh
var _saved_size := Vector3.ZERO

# The red handles edit the BoxMesh in place. Godot can omit those edits when
# saving an instanced scene that still references its inherited mesh.
@export var wall_size: Vector3:
	get:
		var box := get("mesh") as BoxMesh
		if Engine.is_editor_hint() and is_inside_tree() and box != null:
			return box.size
		return _saved_size
	set(value):
		_saved_size = value
		if is_inside_tree():
			var box := get("mesh") as BoxMesh
			if box != null:
				box.size = _wall_dimensions()

func _wall_dimensions() -> Vector3:
	return _saved_size if _saved_size != Vector3.ZERO else Vector3(4, 2, 4)

func _enter_tree() -> void:
	var box := get("mesh") as BoxMesh
	if box == null or box == _editable_mesh:
		return
	if not Engine.is_editor_hint() and _saved_size == Vector3.ZERO:
		return

	# Packed wall instances already have a local mesh, but node duplication can
	# copy a mesh override by reference.
	if box.get_local_scene() != self:
		box = box.duplicate() as BoxMesh
		set("mesh", box)
	_editable_mesh = box
	if _saved_size != Vector3.ZERO:
		box.size = _wall_dimensions()
