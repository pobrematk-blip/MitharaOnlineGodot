@tool
extends EditorPlugin

var ziva_dock: Control

func _enter_tree() -> void:
	ziva_dock = preload("res://addons/ziva_ai/ziva_dock.gd").new()
	add_control_to_dock(DOCK_SLOT_RIGHT_UL, ziva_dock)
	print("[Ziva AI] ✅ Plugin carregado com sucesso!")

func _exit_tree() -> void:
	if ziva_dock:
		remove_control_from_docks(ziva_dock)
		ziva_dock.queue_free()
	print("[Ziva AI] ❌ Plugin descarregado")
