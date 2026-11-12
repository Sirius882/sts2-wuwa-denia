extends SceneTree
## Standalone renderer probe. No game assembly, mod registration or runtime dependency is changed.

const Rig = preload("res://rig.gd")
var output := ""
var frames := 120
var failures: Array[String] = []
var checks := 0

class Board extends Node2D:
	var phase := "IDLE"
	func _draw() -> void:
		draw_rect(Rect2(0, 0, 1200, 720), Color("151621"))
		draw_rect(Rect2(34, 115, 554, 530), Color("212333"))
		draw_rect(Rect2(612, 115, 554, 530), Color("212333"))
		var font := ThemeDB.fallback_font
		draw_string(font, Vector2(36, 49), "DENIA / REALTIME RENDERING LAB", HORIZONTAL_ALIGNMENT_LEFT, -1, 27, Color("f3dce8"))
		draw_string(font, Vector2(36, 83), "Mechanism specimen only - not a final character design or quality comparison", HORIZONTAL_ALIGNMENT_LEFT, -1, 16, Color("a7a4bc"))
		draw_string(font, Vector2(60, 151), "A / CODE GEOMETRY", HORIZONTAL_ALIGNMENT_LEFT, -1, 22, Color("ebafcf"))
		draw_string(font, Vector2(638, 151), "B / FIXED ATLAS + LIVE MESH", HORIZONTAL_ALIGNMENT_LEFT, -1, 22, Color("ebafcf"))
		draw_string(font, Vector2(60, 625), "Drawn color / shadow regions; no lighting solve", HORIZONTAL_ALIGNMENT_LEFT, -1, 16, Color("b6acc3"))
		draw_string(font, Vector2(638, 625), "One 512 x 256 atlas; no frame sequence", HORIZONTAL_ALIGNMENT_LEFT, -1, 17, Color("b6acc3"))
		draw_string(font, Vector2(36, 683), phase + "   /   analytic IK + local spring   /   Dummy physics", HORIZONTAL_ALIGNMENT_LEFT, -1, 18, Color("d8c9df"))
		for x in [310, 890]:
			draw_line(Vector2(x - 120, 596), Vector2(x + 120, 596), Color("5a4563"), 1.0, true)

func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	for i in args.size():
		if args[i] == "--output":
			output = args[i + 1]
		elif args[i] == "--frames":
			frames = int(args[i + 1])
	call_deferred("run", args.has("--self-test"))

func check(condition: bool, message: String) -> void:
	checks += 1
	if not condition:
		failures.append(message)

func run(self_test: bool) -> void:
	if output.is_empty():
		push_error("Pass --output pointing at a directory in the workspace .cache.")
		quit(2)
		return
	DirAccess.make_dir_recursive_absolute(output)
	if self_test:
		run_checks()
		return
	# The launcher keeps the OS window offscreen; unlike headless mode this uses a real GPU.
	root.set_flag(Window.FLAG_NO_FOCUS, true)
	var viewport := SubViewport.new()
	viewport.size = Vector2i(1200, 720)
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var board := Board.new()
	viewport.add_child(board)
	var rigs := [Rig.new(false), Rig.new(true)]
	for i in 2:
		rigs[i].position = Vector2(310 + i * 580, 590)
		rigs[i].scale = Vector2.ONE * 0.87
		board.add_child(rigs[i])
	var costs: Array[float] = []
	var actions := {24: "attack", 61: "guard", 88: "attack", 112: "dead", 128: "revive"}
	for frame in frames:
		if actions.has(frame):
			for rig in rigs:
				rig.play(actions[frame])
		if frame == 78:
			for rig in rigs:
				rig.dark = true
		if frame == 88:
			for rig in rigs:
				rig.speed = 3.0
		var start := Time.get_ticks_usec()
		for rig in rigs:
			rig.step(1.0 / 24.0)
		costs.append((Time.get_ticks_usec() - start) / 1000.0)
		board.phase = "%s / %s / %.1fx" % [rigs[0].state.to_upper(), "DARK" if rigs[0].dark else "PINK", rigs[0].speed]
		board.queue_redraw()
		await process_frame
		await RenderingServer.frame_post_draw
		var capture := viewport.get_texture().get_image()
		if capture == null or capture.is_empty():
			push_error("GPU capture unavailable; a headless Dummy-renderer run is not a visual check.")
			quit(3)
			return
		var error := capture.save_png(output.path_join("frame_%04d.png" % frame))
		if error != OK:
			quit(4)
			return
	costs.sort()
	var result := {
		"engine": Engine.get_version_info().string,
		"renderer": RenderingServer.get_current_rendering_method(),
		"adapter": RenderingServer.get_video_adapter_name(),
		"frames": frames,
		"rig_count": 2,
		"update_cpu_ms_median": costs[costs.size() / 2],
		"update_cpu_ms_p95": costs[int(costs.size() * 0.95)],
		"draw_calls_last_frame": Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME),
		"limit": "GDScript mechanism specimen. CPU update only; PNG capture and GPU cost excluded. Not a game FPS benchmark."
	}
	FileAccess.open(output.path_join("render-result.json"), FileAccess.WRITE).store_string(JSON.stringify(result, "\t"))
	print(JSON.stringify(result))
	for rig in rigs:
		rig.free()
	viewport.free()
	quit()

func run_checks() -> void:
	for target in [Vector2.ZERO, Vector2(0.00001, 0), Vector2(20, 20), Vector2(2000, -2000)]:
		var ik := Rig.solve_ik(Vector2.ZERO, target, 59.0, 57.0, 1.0)
		check(absf(ik[0].distance_to(ik[1]) - 59.0) < 0.005, "IK upper segment length")
		check(absf(ik[1].distance_to(ik[2]) - 57.0) < 0.005, "IK lower segment length")
	for textured in [false, true]:
		var rig := Rig.new(textured)
		root.add_child(rig)
		check(rig.pieces.size() == 14, "Stable layer count")
		for piece in rig.pieces:
			check(piece.node.material == null, "Drawing has no lighting shader")
			if not textured:
				check(piece.node.texture == null, "Code drawing has no character bitmap")
				var colors: PackedColorArray = piece.node.vertex_colors
				check(colors[0].get_luminance() > colors[colors.size() - 1].get_luminance(), "Authored bright and dark colors remain distinct")
		for i in 240:
			if i % 60 == 0:
				rig.play("attack")
			if i == 90:
				rig.dark = true
			rig.step(1.0 / 60.0)
			check(rig.inverted_triangles() == 0, "No inverted triangles at sample %d" % i)
			check(rig.spring.is_finite(), "Finite secondary motion")
		var frozen: float = rig.clock
		rig.paused = true
		rig.step(10.0)
		check(rig.clock == frozen, "Pause freezes local clock")
		rig.paused = false
		rig.step(10.0)
		check(rig.clock - frozen <= 0.101, "Resume bounds catch-up")
		rig.play("dead")
		rig.play("attack")
		check(rig.state == "dead", "Death blocks attacks")
		rig.play("revive")
		check(rig.state == "idle", "Revive restores idle")
		rig.speed = 3.0
		rig.play("attack")
		for i in 25:
			rig.step(1.0 / 60.0)
		check(rig.state == "idle", "Fast action completes")
		check(rig.inverted_triangles() == 0, "Fast action mesh stays valid")
		rig.play("guard")
		rig.step(0.1)
		for piece in rig.pieces:
			if piece.id == "head":
				var head_shift: Vector2 = piece.node.polygon[0] - piece.rest[0]
				check(head_shift.distance_to(rig.face_offset) < 0.001, "Rigid face follows guard translation")
		rig.free()
	var springs: Array[Vector2] = []
	for fps in [30, 60, 144]:
		var rig := Rig.new(false)
		for i in fps * 4:
			rig.step(1.0 / fps)
		springs.append(rig.spring)
		rig.free()
	check(springs[0].distance_to(springs[1]) < 0.003, "30/60 FPS spring consistency")
	check(springs[1].distance_to(springs[2]) < 0.003, "60/144 FPS spring consistency")
	var report := {"checks": checks, "failures": failures, "scope": "Standalone Godot numerical and API checks; no game or RitsuLib runtime tested."}
	FileAccess.open(output.path_join("checks.json"), FileAccess.WRITE).store_string(JSON.stringify(report, "\t"))
	print(JSON.stringify(report))
	quit(0 if failures.is_empty() else 1)
