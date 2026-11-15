extends Node
var checks = 0
func check(value: bool, message: String):
    if not value:
        push_error(message)
        get_tree().quit(1)
    checks += 1
func capture(viewport: SubViewport, name: String):
    await RenderingServer.frame_post_draw
    check(viewport.get_texture().get_image().save_png("res://captures/" + name + ".png") == OK, "Capture " + name)
func stats(background: Control) -> Dictionary:
    return background.call("GetPlaybackStats")
func _ready():
    DirAccess.make_dir_recursive_absolute("res://captures")
    var viewport = SubViewport.new()
    viewport.size = Vector2i(960, 540)
    viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
    add_child(viewport)
    var parent = Control.new()
    parent.size = Vector2(viewport.size) + Vector2(640, 120)
    parent.position = Vector2(-388, -80)
    parent.pivot_offset = Vector2(1280, 600)
    parent.scale = Vector2(1.1, 1.1)
    viewport.add_child(parent)
    var packed = load("res://scenes/screens/char_select/denia_bg.tscn")
    var background = packed.instantiate()
    parent.add_child(background)
    var poster: TextureRect = background.get_node("Poster")
    var hardware: TextureRect = background.get_node("HardwareVideo")
    var fallback: VideoStreamPlayer = background.get_node("Video")
    check(poster.visible and not hardware.visible, "Poster covers asynchronous decoder startup")
    for node in [background, hardware, poster, fallback]:
        check(node.mouse_filter == Control.MOUSE_FILTER_IGNORE, "Background ignores selection input")
    var start = Time.get_ticks_usec()
    while not stats(background)["playing"] and Time.get_ticks_usec() - start < 5000000:
        await RenderingServer.frame_post_draw
    check(stats(background)["playing"] and stats(background)["hardware"], "D3D11 hardware surfaces active")
    check(not fallback.is_playing(), "Theora decoder is never started on hardware path")
    check(stats(background)["width"] == 3840 and stats(background)["height"] == 2160, "Original 4K size retained")
    var previous = 0.0
    var wraps = 0
    var times: Array[float] = []
    var tick = Time.get_ticks_usec()
    var bench_start = tick
    var captured = false
    while Time.get_ticks_usec() - bench_start < 14500000:
        await RenderingServer.frame_post_draw
        var now = Time.get_ticks_usec()
        var ms = (now - tick) / 1000.0
        tick = now
        if now - bench_start > 1000000: times.append(ms)
        var current = stats(background)
        if current["position"] + 0.1 < previous: wraps += 1
        previous = current["position"]
        check(current["visible"] and not poster.visible, "Hardware video remains visible")
        if not captured and current["position"] > .5:
            await capture(viewport, "hardware_video")
            captured = true
    var measured = stats(background)
    check(wraps >= 2, "Original 397-frame timeline loops at least twice")
    parent.hide()
    var stopped = stats(background)["position"]
    await get_tree().create_timer(.2).timeout
    check(abs(stats(background)["position"] - stopped) < .001, "Hidden playback clock freezes")
    parent.show()
    await get_tree().create_timer(.2).timeout
    check(abs(stats(background)["position"] - stopped) > .05, "Visible playback resumes")
    for size in [Vector2i(1280, 800), Vector2i(1280, 540), Vector2i(960, 720)]:
        viewport.size = size
        parent.size = Vector2(size) + Vector2(640, 120)
        await get_tree().process_frame
        await get_tree().process_frame
        await RenderingServer.frame_post_draw
        check(background.size.is_equal_approx(size), "Background follows viewport")
        check(background.get_global_transform().get_scale().is_equal_approx(Vector2.ONE), "Parent parallax scale is canceled")
        check(is_equal_approx(hardware.size.x / hardware.size.y, 16.0 / 9.0), "Aspect ratio retained")
        check(hardware.position.x <= .01 and hardware.position.y <= .01 and hardware.position.x + hardware.size.x >= size.x - .01 and hardware.position.y + hardware.size.y >= size.y - .01, "Centered video fills viewport")
        await capture(viewport, "layout_%dx%d" % [size.x, size.y])
    background.free()
    background = packed.instantiate()
    parent.add_child(background)
    check(stats(background)["presented"] == 0 and stats(background)["position"] == 0, "Reselection starts a new timeline")
    background.free()
    times.sort()
    var sum = 0.0
    var over33 = 0
    for t in times:
        sum += t
        if t > 33.4: over33 += 1
    var report = {"checks":checks,"wraps":wraps,"backend":measured["backend"],"hardware":measured["hardware"],"frames":times.size(),"mean_ms":sum/times.size(),"p95_ms":times[int(times.size()*.95)],"p99_ms":times[int(times.size()*.99)],"over33":over33,"presented":measured["presented"],"skipped":measured["skipped"],"render":DisplayServer.get_name()}
    FileAccess.open("res://captures/checks.json", FileAccess.WRITE).store_string(JSON.stringify(report,"  "))
    print("PASS: ",JSON.stringify(report))
    await get_tree().create_timer(.2).timeout
    get_tree().quit()
