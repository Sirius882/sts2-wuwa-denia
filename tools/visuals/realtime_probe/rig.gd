extends Node2D
## Mechanism specimen, not a proposed Denia illustration or production animation controller.
## Both renderers share joints; one uses vertex colors, the other a fixed texture atlas.

static var atlas: Texture2D
var textured := false
var dark := false
var state := "idle"
var clock := 0.0
var elapsed := 0.0
var speed := 1.0
var motion := 1.0
var secondary := 1.0
var paused := false
var spring := Vector2.ZERO # x = angle, y = angular velocity
var accumulator := 0.0
var pieces: Array[Dictionary] = []
var joints: Dictionary = {}
var aim := Vector2(145, -310)
var face_offset := Vector2.ZERO

func _init(use_texture: bool = false) -> void:
	textured = use_texture
	if textured and atlas == null:
		atlas = ImageTexture.create_from_image(Image.load_from_file("res://probe_atlas.png"))
	# Layer order is explicit, independent of joint hierarchy.
	mesh_piece("hair_l", Rect2(-52, -440, 42, 265), 4, 16, Color("bca4ca"), 0)
	mesh_piece("hair_r", Rect2(25, -440, 46, 290), 4, 16, Color("cbb8d8"), 0)
	for side in [-1, 1]:
		limb_piece("leg_%d_a" % side, Color("ece3e9"), 1)
		limb_piece("leg_%d_b" % side, Color("66505f"), 2)
	mesh_piece("skirt", Rect2(-53, -255, 115, 135), 8, 12, Color("e795b9"), 2)
	mesh_piece("body", Rect2(-30, -348, 70, 104), 6, 9, Color("f1c8da"), 1)
	for side in [-1, 1]:
		limb_piece("arm_%d_a" % side, Color("f2c9da"), 1)
		limb_piece("arm_%d_b" % side, Color("82617d"), 2)
	mesh_piece("head", Rect2(-28, -429, 67, 75), 6, 8, Color("f9e1d5"), 3)
	mesh_piece("fringe", Rect2(-34, -446, 75, 43), 8, 5, Color("c9b6d6"), 0)
	update_pose()

func mesh_piece(id: String, bounds: Rect2, columns: int, rows: int, color: Color, tile: int) -> void:
	var points := PackedVector2Array()
	var uv := PackedVector2Array()
	var colors := PackedColorArray()
	var faces: Array[PackedInt32Array] = []
	for row in range(rows + 1):
		var v := float(row) / rows
		for column in range(columns + 1):
			var u := float(column) / columns
			var width := 1.0
			if id.begins_with("hair"):
				width = 0.30 + 0.70 * sin(PI * (0.12 + 0.86 * v))
			elif id == "skirt":
				width = lerpf(0.53, 1.0, v)
			elif id == "head":
				width = sqrt(maxf(0.0, 1.0 - pow(2.0 * v - 1.0, 2))) * 0.97 + 0.03
			elif id == "body":
				width = 0.67 + 0.25 * absf(2.0 * v - 1.0)
			points.append(bounds.position + Vector2(bounds.size.x * (0.5 + (u - 0.5) * width), bounds.size.y * v))
			uv.append(Vector2(tile * 128 + 2 + u * 124, 2 + v * 252))
			# Paint the designed light and dark regions directly; no scene-light calculation.
			var painted := color.darkened(0.24) if u >= 0.67 else color
			if u <= 0.25:
				painted = painted.lightened(0.08)
			colors.append(painted)
	for row in range(rows):
		for column in range(columns):
			var a := row * (columns + 1) + column
			faces.append(PackedInt32Array([a, a + 1, a + columns + 2]))
			faces.append(PackedInt32Array([a, a + columns + 2, a + columns + 1]))
	var node := Polygon2D.new()
	node.show_behind_parent = true
	node.polygon = points
	node.polygons = faces
	node.antialiased = true
	if textured:
		node.texture = atlas
		node.uv = uv
	else:
		node.vertex_colors = colors
	add_child(node)
	pieces.append({"id": id, "node": node, "rest": points, "faces": faces, "bounds": bounds})

func limb_piece(id: String, color: Color, tile: int) -> void:
	mesh_piece(id, Rect2(-9, 0, 18, 1), 1, 1, color, tile)

func play(action: String) -> void:
	if state == "dead" and action != "revive":
		return
	state = "idle" if action == "revive" else action
	elapsed = 0.0
	if action == "revive":
		spring = Vector2.ZERO

func step(delta: float) -> void:
	if paused:
		return
	# Bound catch-up after stalls. Secondary motion must not use the game's Dummy physics server.
	var dt := minf(maxf(delta, 0.0), 0.10) * speed
	clock += dt
	elapsed += dt
	if state in ["attack", "guard"] and elapsed >= 1.2:
		state = "idle"
	accumulator += dt
	while accumulator >= 1.0 / 120.0:
		var target := -0.12 * attack_weight() + 0.04 * sin(clock * 2.0)
		spring = spring_step(spring, target, 1.0 / 120.0)
		accumulator -= 1.0 / 120.0
	update_pose()

static func spring_step(value: Vector2, target: float, dt: float) -> Vector2:
	value.y += ((target - value.x) * 100.0 - value.y * 16.0) * dt
	value.x += value.y * dt
	return value

func attack_weight() -> float:
	return sin(PI * clampf(elapsed / 1.2, 0.0, 1.0)) if state == "attack" else 0.0

static func solve_ik(origin: Vector2, target: Vector2, length_a: float, length_b: float, bend: float) -> PackedVector2Array:
	var offset := target - origin
	var distance := clampf(offset.length(), absf(length_a - length_b) + 0.001, length_a + length_b - 0.001)
	var direction := offset.normalized() if offset.length_squared() > 0.000001 else Vector2.DOWN
	var along := (length_a * length_a - length_b * length_b + distance * distance) / (2.0 * distance)
	var height := sqrt(maxf(0.0, length_a * length_a - along * along))
	return PackedVector2Array([origin, origin + direction * along + direction.orthogonal() * height * bend, origin + direction * distance])

func update_pose() -> void:
	var attack := attack_weight() * motion
	var guard := sin(PI * clampf(elapsed / 1.2, 0.0, 1.0)) * motion if state == "guard" else 0.0
	var bob := Vector2(attack * 10.0 - guard * 12.0, sin(clock * TAU / 4.0) * 1.8 * motion)
	face_offset = bob
	var dead := smoothstep(0.0, 0.8, elapsed) if state == "dead" else 0.0
	rotation = dead * 1.30
	joints.clear()
	for side in [-1, 1]:
		var hip := Vector2(side * 17, -145) + bob
		var foot := Vector2(side * 24 + 4, -4)
		var leg := solve_ik(hip, foot, 77.0, 77.0, float(side))
		joints["leg_%d" % side] = leg
		var shoulder := Vector2(side * 29 + 3, -329) + bob
		var hand := Vector2(side * 42 + 5, -235) + bob
		if side == 1:
			hand = hand.lerp(aim + bob, attack)
		hand = hand.lerp(Vector2(side * 21 + 8, -320) + bob, guard)
		joints["arm_%d" % side] = solve_ik(shoulder, hand, 59.0, 57.0, float(side))
	for piece in pieces:
		var id: String = piece.id
		var node: Polygon2D = piece.node
		var rest: PackedVector2Array = piece.rest
		var points := rest.duplicate()
		if id.begins_with("arm") or id.begins_with("leg"):
			var split := id.split("_")
			var joint: PackedVector2Array = joints[split[0] + "_" + split[1]]
			var start := 0 if split[2] == "a" else 1
			var a := joint[start]
			var b := joint[start + 1]
			var normal := (b - a).normalized().orthogonal() * (9.0 if split[0] == "arm" else 11.0)
			points = PackedVector2Array([a - normal, a + normal, b - normal, b + normal])
		else:
			for i in points.size():
				var v: float = (rest[i].y - piece.bounds.position.y) / piece.bounds.size.y
				points[i] += bob
				if id.begins_with("hair"):
					points[i].x += secondary * (spring.x * 160.0 * v * v + sin(clock * 2.2 - v * 3.0) * 7.0 * v * v)
				elif id == "skirt":
					points[i].x += secondary * sin(clock * 2.0 - v * 2.0) * 5.0 * v * v + attack * 9.0 * v
				elif id == "body":
					points[i].x += sin(clock * TAU / 4.0) * 0.9 * sin(v * PI) * motion
		# A palette changes directly; no transition animation is implied.
		node.modulate = Color("907bb5") if dark and id not in ["head"] else Color.WHITE
		node.polygon = points
	queue_redraw()

func _draw() -> void:
	# Rigid face features follow translation, never chest scaling.
	var bob := face_offset
	# A drawn fringe shadow follows the face; it is a colored shape, not a solved projection.
	var fringe_shadow := PackedVector2Array([Vector2(-23, -402), Vector2(34, -402), Vector2(30, -394), Vector2(-20, -391)])
	for i in fringe_shadow.size():
		fringe_shadow[i] += bob
	draw_colored_polygon(fringe_shadow, Color("d7b3b9"))
	var blink := 0.12 if fmod(clock, 3.8) > 3.65 else 1.0
	for x in [-8, 19]:
		draw_ellipse(Vector2(x, -388) + bob, Vector2(4, 6 * blink), Color("593c65"))
	draw_line(Vector2(2, -371) + bob, Vector2(12, -371) + bob, Color("a96681"), 1.5, true)
	draw_arc(Vector2(4, -435) + bob, 42.0, PI * 1.05, PI * 1.95, 28, Color("f0b7d6"), 5.0, true)

func draw_ellipse(center: Vector2, radii: Vector2, color: Color) -> void:
	var points := PackedVector2Array()
	for i in 24:
		points.append(center + Vector2(cos(i * TAU / 24) * radii.x, sin(i * TAU / 24) * radii.y))
	draw_colored_polygon(points, color)

func inverted_triangles() -> int:
	var count := 0
	for piece in pieces:
		var points: PackedVector2Array = piece.node.polygon
		for face in piece.faces:
			if (points[face[1]] - points[face[0]]).cross(points[face[2]] - points[face[0]]) <= 0.00001:
				count += 1
	return count
