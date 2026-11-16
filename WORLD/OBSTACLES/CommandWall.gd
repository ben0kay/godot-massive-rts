@tool
extends "res://WORLD/TERRAIN/AUTHORING/CommandSurfaceSnap.gd"
## Gives each editable wall its own box dimensions while sharing the material.

var _editable_mesh: BoxMesh

func _enter_tree() -> void:
	if not Engine.is_editor_hint():
		return
	var box := get("mesh") as BoxMesh
	if box == null or box == _editable_mesh:
		return
	# Packed scene instances already get a local mesh. Node duplication can copy
	# a mesh override by reference, so take a private copy before it is edited.
	if box.get_local_scene() != self:
		box = box.duplicate() as BoxMesh
		set("mesh", box)
	_editable_mesh = box
