extends PanelContainer

var http_client: HTTPClient
var api_key: String = ""
var is_authenticated: bool = false

func _ready() -> void:
	custom_minimum_size = Vector2(300, 400)
	
	var vbox = VBoxContainer.new()
	add_child(vbox)
	
	# Título
	var title = Label.new()
	title.text = "🤖 Ziva AI"
	title.add_theme_font_size_override("font_size", 16)
	vbox.add_child(title)
	
	# Linha de separação
	vbox.add_child(HSeparator.new())
	
	# Campo de API Key
	var api_label = Label.new()
	api_label.text = "API Key:"
	vbox.add_child(api_label)
	
	var api_key_input = LineEdit.new()
	api_key_input.secret = true
	api_key_input.placeholder_text = "Cole sua API key aqui"
	api_key_input.text_changed.connect(func(text): api_key = text)
	vbox.add_child(api_key_input)
	
	# Botão de conectar
	var connect_btn = Button.new()
	connect_btn.text = "🔗 Conectar"
	connect_btn.pressed.connect(_on_connect_pressed)
	vbox.add_child(connect_btn)
	
	# Status
	var status_label = Label.new()
	status_label.name = "StatusLabel"
	status_label.text = "⚪ Desconectado"
	status_label.add_theme_color_override("font_color", Color.YELLOW)
	vbox.add_child(status_label)
	
	# Campo de prompt
	var prompt_label = Label.new()
	prompt_label.text = "Seu prompt:"
	vbox.add_child(prompt_label)
	
	var prompt_input = TextEdit.new()
	prompt_input.name = "PromptInput"
	prompt_input.custom_minimum_size = Vector2(280, 100)
	prompt_input.placeholder_text = "Ex: Crie um sistema de inimigos com IA"
	vbox.add_child(prompt_input)
	
	# Botão enviar
	var send_btn = Button.new()
	send_btn.text = "✉️ Enviar"
	send_btn.pressed.connect(_on_send_pressed.bind(prompt_input))
	vbox.add_child(send_btn)
	
	# Resposta
	var response_label = Label.new()
	response_label.text = "Resposta:"
	vbox.add_child(response_label)
	
	var response_display = TextEdit.new()
	response_display.name = "ResponseDisplay"
	response_display.custom_minimum_size = Vector2(280, 150)
	response_display.editable = false
	vbox.add_child(response_display)

func _on_connect_pressed() -> void:
	if api_key.is_empty():
		_update_status("❌ API key vazia!", Color.RED)
		return
	
	is_authenticated = true
	_update_status("✅ Conectado!", Color.GREEN)
	print("[Ziva AI] ✅ Autenticado com sucesso!")

func _on_send_pressed(prompt_input: TextEdit) -> void:
	if not is_authenticated:
		_update_status("❌ Não autenticado!", Color.RED)
		return
	
	if prompt_input.text.is_empty():
		_update_status("❌ Prompt vazio!", Color.RED)
		return
	
	_update_status("⏳ Enviando...", Color.YELLOW)
	
	# Aqui você conectaria com a API real do Ziva
	var response = "[Resposta do Ziva AI]\n\n" + prompt_input.text
	
	var response_display = find_child("ResponseDisplay")
	if response_display:
		response_display.text = response
	
	_update_status("✅ Pronto!", Color.GREEN)

func _update_status(message: String, color: Color) -> void:
	var status_label = find_child("StatusLabel")
	if status_label:
		status_label.text = message
		status_label.add_theme_color_override("font_color", color)
