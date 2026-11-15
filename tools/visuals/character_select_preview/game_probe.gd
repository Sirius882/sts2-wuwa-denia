extends SceneTree

# Run through the installed game's normal mod initializer, not a standalone preview assembly.
# Do not open a run or change character selections/saves.
func _initialize():
    call_deferred("probe")

func require(value: bool, message: String) -> bool:
    if not value:
        push_error("DENIA_BG_GAME: " + message)
        quit(1)
    return value

func probe():
    var main_path = ProjectSettings.get_setting("application/run/main_scene")
    var main = load(main_path).instantiate()
    root.add_child(main)
    current_scene = main
    await create_timer(12.0).timeout
    var packed = load("res://scenes/screens/char_select/denia_bg.tscn")
    if not require(packed != null, "Installed background scene is mounted"):
        return
    var background = packed.instantiate()
    root.add_child(background)
    await process_frame
    var script = background.get_script()
    if not require(script != null and script.can_instantiate(), "Denia C# script is registered by mod initialization"):
        return
    var poster = background.get_node("Poster") as TextureRect
    var start = Time.get_ticks_usec()
    while not background.call("GetPlaybackStats")["playing"] and Time.get_ticks_usec() - start < 5000000:
        await process_frame
    var stats = background.call("GetPlaybackStats")
    await create_timer(0.5).timeout
    stats = background.call("GetPlaybackStats")
    if not require(stats["playing"] and stats["position"] > 0 and stats["visible"] and not poster.visible and stats["hardware"],
            "Installed video uses hardware decoding, advances and replaces the poster"):
        return
    if not require(background.size.is_equal_approx(background.get_viewport_rect().size), "Driver sizes background to viewport"):
        return
    if DisplayServer.get_name() != "headless":
        var tick = Time.get_ticks_usec()
        var begin = tick
        var times: Array[float] = []
        var wraps = 0
        var previous = stats["position"]
        while Time.get_ticks_usec() - begin < 14000000:
            await process_frame
            var now = Time.get_ticks_usec()
            if now - begin > 1000000: times.append((now - tick) / 1000.0)
            tick = now
            stats = background.call("GetPlaybackStats")
            if stats["position"] + .1 < previous: wraps += 1
            previous = stats["position"]
        times.sort()
        var sum = 0.0
        var over33 = 0
        for t in times:
            sum += t
            if t > 33.4: over33 += 1
        var report = {"frames":times.size(),"mean_ms":sum/times.size(),"p95_ms":times[int(times.size()*.95)],"p99_ms":times[int(times.size()*.99)],"over33":over33,"wraps":wraps,"playback":stats}
        print("DENIA_BG_GAME_TIMING ",JSON.stringify(report))
        if not require(wraps >= 2, "Installed GPU playback loops twice"):
            return
    background.hide()
    stats = background.call("GetPlaybackStats")
    var stopped = stats["position"]
    await create_timer(0.2).timeout
    if not require(abs(background.call("GetPlaybackStats")["position"] - stopped) < 0.001, "Hidden background pauses decoding"):
        return
    background.show()
    await create_timer(0.2).timeout
    if not require(background.call("GetPlaybackStats")["position"] > stopped, "Visible background resumes decoding"):
        return
    print("DENIA_BG_GAME PASS: registered scene; hardware native video visible and advancing; poster/layout/hide/resume")
    background.free()
    quit()
