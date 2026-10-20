"""Functional districts for the authored KursHub, in Unity-local metres.

This module has no Blender, Unity, filesystem, or random dependency.  Call
``augment`` on the caller's manifest copy; existing placement dictionaries and
their order are retained.  Revision-8 additions use stable ``Hub8`` identifiers.
The returned data is an inspection/authoring contract, not a NavMesh or a claim
that a real controller has already traversed the proposed routes.
"""

from math import cos, sin, radians


# Conservative authoring envelopes, including handles, open cabinet door and
# tube radii.  These help review the layout; imported colliders remain authority.
PROP_ENVELOPES = {
    "KH_DieselGenerator": (-1.495, 1.4, -.67, .595, 1.607),
    "KH_PowerSwitchboard": (-.8, .8, -.352, .225, 2.109),
    "KH_WaterTreatment": (-1.58, 1.593, -.57, .57, 2.014),
    "KH_UtilityRack": (-1.25, 1.25, -.405, .405, 2.2),
    "KH_RepairBench": (-1.25, 1.25, -.506, .446, 1.783),
    "KH_RadioDesk": (-.75, .75, -.4, .375, 1.244),
    "KH_MedicalCot": (-.54, .54, -1.06, 1.06, 1.02),
    "KH_MedicalCabinet": (-.66, .66, -.53, .24, 1.81),
    "KH_KitchenRange": (-1.11, 1.11, -.51, .43, 1.4),
    "KH_WashStation": (-.91, .91, -.43, .36, 1.52),
    "KH_PersonalLocker": (-.76, .76, -.36, .26, 1.91),
    "KH_CommunityBoard": (-1.11, 1.11, -.1, .1, 1.81),
    "KH_StallCounter": (-1.5, 1.505, -.679, .569, 1.055),
    "KH_PalletCrates": (-.75, .75, -.625, .615, 1.42),
    "KH_SupplySackStack": (-.74, .71, -.526, .493, .96),
    "KH_BunkMakeshift": (-1.164, 1.171, -.718, .573, 2.12),
    "KH_Bedroll": (-1.04, 1.04, -.46, .5, .435),
    "KH_RestTable": (-1., 1., -.447, .447, 1.163),
    "KH_Bench": (-.9, .9, -.32, .328, .883),
    "KH_Stool": (-.235, .252, -.245, .245, .524),
    "KH_CampChair": (-.303, .328, -.323, .370, .918),
    "KH_ResidentBundle": (-.555, .66, -.5, .399, .711),
    "KH_PottedPlant": (-.261, .234, -.330, .307, 1.04),
    "KH_LivingScreen": (-1.35, 1.35, -.195, .245, 2.45),
    "KH_LowDisplay": (-1.125, 1.125, -.7, .7, .66),
    "KH_WorkbenchStocked": (-1.4, 1.4, -.583, .583, 2.355),
}


def _aabb(placement, envelope):
    x0, x1, z0, z1, height = envelope
    sx, sy, sz = placement["scale"]
    a = radians(placement["rotation"][1])
    points = [(x*sx*cos(a)+z*sz*sin(a),
               -x*sx*sin(a)+z*sz*cos(a))
              for x in (x0, x1) for z in (z0, z1)]
    x, y, z = placement["position"]
    return [min(p[0] for p in points)+x, max(p[0] for p in points)+x,
            min(p[1] for p in points)+z, max(p[1] for p in points)+z,
            y, y+height*sy]


def augment(manifest):
    """Append authored furnishings and return the complete spatial audit plan.

    Idempotent for its own additions.  An existing ``Hub8`` placement with
    different values is refused, so rerunning cannot erase a manual adjustment.
    The only changes to old zone records are real ``use`` values and an added
    ``hub8`` design record.  No old placement, light or architectural chunk is
    removed, replaced, reordered, or moved.
    """
    old_groups = {key: list(manifest.get(key, [])) for key in
                  ("propInstances", "interiorInstances", "settlementInstances")}
    items = manifest.setdefault("settlementInstances", [])
    all_items = [p for group in old_groups.values() for p in group]
    by_id = {p["id"]: p for p in all_items}
    if len(by_id) != len(all_items):
        raise ValueError("Duplicate existing KursHub placement identifiers")
    zones, routes, cameras, exclusions, story, additions = [], [], [], [], [], []
    roles, furniture = {}, []

    def put(zone, name, prop, position, yaw=0, scale=(1, 1, 1), role="furniture"):
        identifier = "Hub8 " + zone + " " + name
        record = dict(id=identifier, prop=prop,
                      position=[round(float(v), 5) for v in position],
                      rotation=[0, float(yaw), 0], scale=list(scale))
        if identifier in by_id:
            if by_id[identifier] != record:
                raise ValueError("Preserve manually modified placement: " + identifier)
        else:
            items.append(record)
            by_id[identifier] = record
        additions.append(identifier)
        roles[identifier] = role
        if role == "furniture" and prop in PROP_ENVELOPES:
            furniture.append((identifier, _aabb(record, PROP_ENVELOPES[prop])))
        return record

    def route(identifier, points, width=1.6, purpose="district access"):
        routes.append(dict(id="Hub8 " + identifier, points=[list(p) for p in points],
                           clearWidth=width, purpose=purpose,
                           validation="Proposed path; real imported-collider Play test required"))

    def camera(identifier, position, target, purpose, fov=66):
        cameras.append(dict(id="Hub8 " + identifier, position=list(position),
                            target=list(target), fieldOfView=fov, purpose=purpose))

    def zone(identifier, use, bounds, y, purpose, priority=1):
        row = dict(id=identifier, use=use, floorY=y, bounds=list(bounds),
                   purpose=purpose, priority=priority, placementIds=[])
        zones.append(row)
        return row

    def box(identifier, bounds, y, reason, height=2.3):
        exclusions.append(dict(id=identifier, type="clear footprint", floorY=y,
                               bounds=list(bounds), clearHeight=height, reason=reason))

    def fixtures(identifier, xc, y, south_z):
        # Native cable span is exactly 10 m, with real end mounts.  The mounts
        # touch the inside face of the existing 12 m south partition wall.
        put(identifier, "South wall feeder", "KH_CableSag",
            (xc, y+3.35, south_z+.115), role="fixture")
        put(identifier, "South wall task lamp", "KH_Floodlight",
            (xc, y+2.65, south_z+.126), 180, role="fixture")

    room_uses = {
        "Ground West 01": ("Ration cooperative", "Food is received, counted, portioned and issued here.",
                           "KH_StallCounter", "KH_KitchenRange", "KH_GoodsFood"),
        "Ground West 02": ("Clothing and footwear repair", "Clothes are sorted before mending, not discarded as general debris.",
                           "KH_StallCounter", "KH_RepairBench", "KH_GoodsGear"),
        "Ground West 03": ("Household supply cooperative", "Stock, packing and the public sales face have separate places.",
                           "KH_StallCounter", "KH_UtilityRack", "KH_GoodsTools"),
        "Ground East 01": ("Weapons maintenance workshop", "A working repair bench and controlled static display support the existing weapons market.",
                           "KH_RepairBench", "KH_PersonalLocker", "KH_GoodsTools"),
        "Ground East 02": ("Electrical and mechanical repair", "Failed equipment is inspected and repaired before it returns to the settlement.",
                           "KH_RepairBench", "KH_PowerSwitchboard", "KH_GoodsTools"),
        "Ground East 03": ("Carpentry and plumbing workshop", "Reusable timber, fittings and practical repair work are grouped by activity.",
                           "KH_RepairBench", "KH_WashStation", "KH_GoodsTools"),
    }

    # Keep the old two-door circulation loop in every ground suite.  New work
    # fronts sit BETWEEN portals; new back stock stays beyond the X=±33 aisle.
    for old_zone in manifest.get("interiorZones", []):
        identifier = old_zone["id"]
        x0, x1, z0, z1 = old_zone["bounds"]
        side = -1 if x1 < 0 else 1
        c, y = (z0+z1)/2, old_zone["floorY"]
        facing = side*90
        if identifier in room_uses:
            use, purpose, main_prop, south_prop, goods = room_uses[identifier]
            old_zone["use"] = use
            row = zone(identifier, use, (x0, x1, z0, z1), y, purpose, 0)
            put(identifier, "Public working front", main_prop, (side*30.15, y, c), facing)
            if main_prop == "KH_StallCounter":
                put(identifier, "Counted counter stock", goods,
                    (side*30.15, y+1.055, c), facing, (.82, .82, .82), "surface stock")
            put(identifier, "Reserve shelf", "KH_UtilityRack", (side*38.8, y, c), facing)
            put(identifier, "South service station", south_prop, (side*35.75, y, c-5.1), 180)
            put(identifier, "Front north locker", "KH_PersonalLocker", (side*30.15, y, c+5.05))
            put(identifier, "Front south counted crate", "KH_PalletCrates", (side*30.15, y, c-5.05))
            for index, dz in enumerate((-1.02, 1.02), 1):
                put(identifier, "Worker stool %02d" % index, "KH_Stool",
                    (side*31.35, y, c+dz), facing)
            put(identifier, "Packed customer order", "KH_ResidentBundle",
                (side*28.85, y, c-5.25), 13*side, (.55, .55, .55))
            put(identifier, "Back corner personal kit", "KH_ResidentBundle",
                (side*39.1, y, c+5.1), -17*side, (.6, .6, .6))
            put(identifier, "Working mat", "KH_RugWoven", (side*30.4, y+.007, c), facing)
            put(identifier, "South work rug", "KH_RugBlue" if side > 0 else "KH_RugRed",
                (side*35.75, y+.008, c-4.45), 7*side, (.82, 1, .70), "soft floor")
            put(identifier, "Kept front plant", "KH_PottedPlant", (side*28.75, y, c+5.25), 31*side)
            put(identifier, "South sorted parts", "KH_LowDisplay", (side*39, y, c-5.03), 90,
                (.62, .70, .62))
            fixtures(identifier, side*34, y, z0)
        elif identifier.startswith("Upper West Residence"):
            number = int(identifier[-2:])
            use = ("Family sleeping room", "Communal dormitory", "Quiet family room")[number-1]
            purpose = ("Personal sleeping niches surround a shared table and a usable wash point; "
                       "the old two bunks and belongings remain in place.")
            old_zone["use"] = use
            row = zone(identifier, use, (x0, x1, z0, z1), y, purpose, 0)
            for index, dz in enumerate((-3.45, 3.45), 1):
                put(identifier, "Front sleeping niche %02d" % index,
                    "KH_BunkMakeshift" if number != 3 or index == 1 else "KH_Bedroll",
                    (-30.15, y, c+dz))
                put(identifier, "Niche personal bag %02d" % index, "KH_ResidentBundle",
                    (-30.15, y, c+(-2.12 if dz < 0 else 2.12)), 13*index,
                    (.65, .65, .65))
            put(identifier, "Shared personal lockers", "KH_PersonalLocker", (-38.75, y, c), -90)
            put(identifier, "Private wash point", "KH_WashStation", (-35.75, y, c-5.1), 180)
            put(identifier, "Front north household crate", "KH_PalletCrates", (-30.15, y, c+5.12))
            put(identifier, "Front south houseplant", "KH_PottedPlant", (-30.15, y, c-5.25), 27)
            put(identifier, "Table east stool", "KH_Stool", (-34.65, y, c+.75), -90)
            put(identifier, "Table west stool", "KH_Stool", (-36.35, y, c-.75), 90)
            put(identifier, "Family rug", "KH_RugRed" if number != 2 else "KH_RugBlue",
                (-30.25, y+.009, c), 11-number*7, (1, 1, .8), "soft floor")
            put(identifier, "Rear south keepsakes", "KH_ResidentBundle", (-39.1, y, c-5.1), 9,
                (.55, .55, .55))
            put(identifier, "Rear north stored blankets", "KH_SupplySackStack", (-38.7, y, c+4.9), 90,
                (.6, .6, .6))
            fixtures(identifier, -34, y, z0)
        elif identifier.startswith("Upper East Medical"):
            use = "Medical consultation and triage" if identifier.endswith("01") else "Medical recovery ward"
            purpose = ("New clinical beds, clean storage and handwashing make a treatment room; "
                       "retained floor bedding is emergency reserve, not the main examination furniture.")
            old_zone["use"] = use
            row = zone(identifier, use, (x0, x1, z0, z1), y, purpose, 0)
            for index, dz in enumerate((-3.2, 3.2), 1):
                put(identifier, "Treatment cot %02d" % index, "KH_MedicalCot", (30.4, y, c+dz))
                put(identifier, "Clean bedside locker %02d" % index, "KH_PersonalLocker",
                    (29.25, y, c+dz), 90)
                put(identifier, "Back clean cabinet %02d" % index, "KH_MedicalCabinet",
                    (38.8, y, c+(-4.25 if dz < 0 else 4.25)), 90)
            put(identifier, "Clinical handwashing", "KH_WashStation", (35.5, y, c-5.1), 180)
            put(identifier, "Visitor chair south", "KH_CampChair", (30.4, y, c-5.25), 12)
            put(identifier, "Visitor chair north", "KH_CampChair", (30.4, y, c+5.15), 168)
            put(identifier, "Visitor belongings", "KH_ResidentBundle", (29.1, y, c+5.1), 23,
                (.55, .55, .55))
            put(identifier, "Washable entrance runner", "KH_RugWoven", (33.2, y+.007, c), 0,
                (.8, 1, .8), "soft floor")
            put(identifier, "Clean stock basket", "KH_PalletCrates", (38.6, y, c), 0,
                (.45, .45, .45))
            fixtures(identifier, 34, y, z0)
        else:
            continue

        old_zone["hub8"] = dict(purpose=purpose, mainAisleX=side*33,
                                mainAisleClearWidth=1.6, preservedEntrances=True,
                                rearCorners="Retained stores plus differentiated new service/personal objects",
                                cleanliness="Kept working zone; isolated edge wear, not uniform loose refuse")
        if y == 0:
            points = [[side*23, y, c-3], [side*33, y, c-3],
                      [side*33, y, c+3], [side*23, y, c+3]]
            route(identifier + " preserved two-door loop", points, 1.6, use)
            route(identifier + " front work approach",
                  [[side*33, y, c], [side*32, y, c]], 1.25, "Working side of the new front station")
        else:
            points = old_zone["route"]
            route(identifier + " preserved entry", points, 1.35, use)
            route(identifier + " south activity",
                  [[side*33, y, c], [side*33, y, c-3.4]], 1.35, "Bedside and wash-point approach")
            route(identifier + " north activity",
                  [[side*33, y, c], [side*33, y, c+3.4]], 1.35, "Bedside and personal storage approach")
        # The aisle remains useful space; it is not declared an empty room.
        box(identifier + " internal aisle", sorted((side*32.2, side*33.8))+[c-3.9, c+3.9], y,
            "Preserved old internal room route and two-way access")
        for index, entrance in enumerate(old_zone["entrances"], 1):
            ex, _, ez = entrance
            box(identifier + " entrance %02d" % index,
                sorted((ex-side*5.5, side*33))+[ez-.8, ez+.8], y,
                "Real opening and the retained approach; no doorway furniture")
        camera(identifier + " overview", (side*33, y+1.65, c),
               (side*37.6, y+1.1, c), "Player-height view into the working or inhabited room")
        for label, dz in (("south rear corner", -4.9), ("north rear corner", 4.9)):
            camera(identifier + " " + label, (side*33, y+1.65, c+(-2 if dz < 0 else 2)),
                   (side*38.7, y+.85, c+dz), "Inspect rear corners beyond the main photographed aisle")
        camera(identifier + " reverse entry", (side*33, y+1.65, c+1.2),
               (side*27, y+1.1, c), "Check clear return through the real entrance")

    # The districts below use activity islands, leaving generous connecting
    # space.  Objects are grouped by a reason, never by a building-wide grid.
    for side, label in ((-1, "West"), (1, "East")):
        identifier = "Ground front " + label
        zone(identifier, "Arrival registration" if side < 0 else "Emergency intake",
             sorted((side*8, side*23))+[-57, -41], 0,
             "A waiting and intake sequence occupies the daylight arrival bay without narrowing the central entry.", 0)
        put(identifier, "Staff station", "KH_RadioDesk" if side < 0 else "KH_MedicalCot",
            (side*16, 0, -48), side*90)
        put(identifier, "Service storage", "KH_PersonalLocker" if side < 0 else "KH_MedicalCabinet",
            (side*20.5, 0, -46.5), side*90)
        put(identifier, "Local notice board", "KH_CommunityBoard", (side*12, 0, -43.5))
        for index, z in enumerate((-52.2, -55.8), 1):
            put(identifier, "Waiting bench %02d" % index, "KH_Bench", (side*11.5, 0, z), side*90)
            put(identifier, "Waiting bag %02d" % index, "KH_ResidentBundle",
                (side*13, 0, z+.5), side*17, (.75, .75, .75))
        put(identifier, "Staff stool", "KH_Stool", (side*17.4, 0, -48), side*90)
        put(identifier, "Kept waiting plant", "KH_PottedPlant", (side*20.5, 0, -52.5), side*29)
        put(identifier, "Arrival runner", "KH_RugWoven", (side*15.5, .008, -50), side*9,
            (1.2, 1, 1.1), "soft floor")
        route(identifier + " working loop", [[side*8, 0, -56], [side*8, 0, -48],
                                              [side*14, 0, -45], [side*19, 0, -43]], 1.8)
        camera(identifier, (side*8, 1.65, -52), (side*18, 1.1, -47), "Arrival function and unobstructed central route")

        identifier = "Ground outer front " + label
        zone(identifier, "Quartermaster and guard rest" if side < 0 else "First aid reserve",
             sorted((side*30, side*44))+[-48, -36], 0,
             "The formerly bare front corner supports the arrival staff rather than adding another identical market stall.")
        put(identifier, "Support workstation", "KH_RepairBench" if side < 0 else "KH_WashStation",
            (side*40.5, 0, -42), side*90)
        put(identifier, "Reserve shelf", "KH_UtilityRack" if side < 0 else "KH_MedicalCabinet",
            (side*40.5, 0, -46.2), side*90)
        put(identifier, "Off duty table", "KH_RestTable", (side*34, 0, -42), 7*side)
        put(identifier, "Off duty seat south", "KH_CampChair", (side*34, 0, -43.3), 0)
        put(identifier, "Off duty seat north", "KH_CampChair", (side*34, 0, -40.7), 180)
        put(identifier, "Staff belongings", "KH_ResidentBundle", (side*35.9, 0, -42.6), -13*side)
        put(identifier, "Reserve counted crate", "KH_PalletCrates", (side*41, 0, -38), 11*side)
        route(identifier + " access", [[side*29, 0, -36], [side*32, 0, -38],
                                       [side*37.5, 0, -38], [side*38, 0, -43]], 1.8)
        camera(identifier, (side*32, 1.65, -37), (side*40.5, 1.1, -43), "A whole front corner rather than the market centre")

    identifier = "Ground rear mess"
    zone(identifier, "Communal mess and assembly", (-16, 16, 48, 63), 0,
         "Four table islands permit everyday meals and meetings while the centre remains an assembly/delivery aisle.", 0)
    for index, (x, z, yaw) in enumerate(((-9, 52.5, 6), (9, 52.5, -5),
                                         (-9, 59, -4), (9, 59, 8)), 1):
        put(identifier, "Meal table %02d" % index, "KH_RestTable", (x, 0, z), yaw)
        put(identifier, "Meal bench south %02d" % index, "KH_Bench", (x, 0, z-1.1), yaw)
        put(identifier, "Meal bench north %02d" % index, "KH_Bench", (x, 0, z+1.1), 180+yaw)
        put(identifier, "Personal meal kit %02d" % index, "KH_ResidentBundle",
            (x+1.5, 0, z+.3), 15*index, (.55, .55, .55))
    put(identifier, "Community notices", "KH_CommunityBoard", (-5, 0, 62.4))
    put(identifier, "Meal issue reserve", "KH_SupplySackStack", (-14, 0, 60.6), 12)
    put(identifier, "Returned containers", "KH_PalletCrates", (14, 0, 60.6), -9)
    route(identifier + " centre", [[0, 0, 47], [0, 0, 55], [0, 0, 62]], 4,
          "Meals, community assembly and delivery; intentionally clear centre")
    camera(identifier, (0, 1.65, 49), (-9, 1.05, 57), "Shared activity islands and clear central axis")

    for side, label in ((-1, "Kitchen"), (1, "Water and power")):
        identifier = "Ground rear " + label
        zone(identifier, "Kitchen and larder" if side < 0 else "Water and electrical services",
             sorted((side*18, side*44))+[48, 63], 0,
             "Working equipment, consumable reserve and service access make an operational rear district.", 0)
        if side < 0:
            for name, prop, x, z, yaw in (
                    ("Cooking line", "KH_KitchenRange", -40.5, 52, -90),
                    ("Clean wash line", "KH_WashStation", -40.5, 58, -90),
                    ("Dry food rack", "KH_UtilityRack", -24, 61.4, 0),
                    ("Preparation counter", "KH_StallCounter", -31, 52, 180),
                    ("Portioning counter", "KH_StallCounter", -24, 52, 180),
                    ("Food sacks", "KH_SupplySackStack", -23, 58, 11),
                    ("Produce containers", "KH_PalletCrates", -29, 61.2, -8),
                    ("Kitchen shift locker", "KH_PersonalLocker", -42.5, 61.5, -90)):
                put(identifier, name, prop, (x, 0, z), yaw)
            put(identifier, "Counted preparation food", "KH_GoodsFood", (-31, 1.055, 52), 180,
                (.8, .8, .8), "surface stock")
            put(identifier, "Portioned food", "KH_GoodsFood", (-24, 1.055, 52), 180,
                (.65, .65, .65), "surface stock")
        else:
            for name, prop, x, z, yaw in (
                    ("Water filtration", "KH_WaterTreatment", 40.5, 58, 90),
                    ("Electrical distribution", "KH_PowerSwitchboard", 40.5, 51.5, 90),
                    ("Service reserve rack", "KH_UtilityRack", 24, 61.4, 0),
                    ("Maintenance bench", "KH_RepairBench", 31, 51.5, 180),
                    ("Service wash point", "KH_WashStation", 24, 51.5, 180),
                    ("Equipment cases", "KH_PalletCrates", 29, 61.2, 8),
                    ("Filter consumables", "KH_SupplySackStack", 23, 58, -11),
                    ("Maintenance shift locker", "KH_PersonalLocker", 42.5, 61.5, 90)):
                put(identifier, name, prop, (x, 0, z), yaw)
        put(identifier, "Maintenance stool", "KH_Stool", (side*31, 0, 53.4), 180)
        put(identifier, "Task mat", "KH_RugWoven", (side*40.2, .008, 55), side*90,
            (1.1, 1, 1.2), "soft floor")
        put(identifier, "North wall feeder", "KH_CableSag", (side*34, 4.1, 64.64), role="fixture")
        put(identifier, "North wall task lamp", "KH_Floodlight", (side*34, 3.1, 64.664), role="fixture")
        route(identifier + " equipment loop", [[side*19, 0, 48], [side*19, 0, 55],
                                               [side*34, 0, 55], [side*38, 0, 55],
                                               [side*38, 0, 61]], 2)
        camera(identifier, (side*24, 1.65, 55), (side*40.5, 1.2, 56), "Full service sequence including rear corners")

    # Rear workshop/store islands flank rather than obstruct the old rear stalls.
    for side, label in ((-1, "West"), (1, "East")):
        identifier = "Ground rear stores " + label
        zone(identifier, "Sorted repair reserve", sorted((side*28, side*44))+[20, 44], 0,
             "Shelving and repair stock occupy the rear edges; the transverse delivery aisle is intentionally open.")
        for index, (x, z) in enumerate(((40.5, 28.5), (40.5, 36), (30.7, 36)), 1):
            put(identifier, "Reserve rack %02d" % index, "KH_UtilityRack", (side*x, 0, z), side*90)
            put(identifier, "Counted floor cases %02d" % index, "KH_PalletCrates",
                    (side*x, 0, z+2.9), side*(7+index*3), (.8, .8, .8))
        put(identifier, "Repair sorting bench", "KH_RepairBench", (side*41, 0, 42), side*90)
        put(identifier, "Sorted sacks", "KH_SupplySackStack", (side*30.5, 0, 30), -13*side)
        route(identifier + " delivery", [[side*28, 0, 23], [side*33.3, 0, 23],
                                         [side*37.8, 0, 28], [side*37.8, 0, 40]], 2)
        camera(identifier, (side*34, 1.65, 33), (side*41, 1.3, 38), "Rear edge between repeated old stalls")

    identifier = "Upper front common room"
    zone(identifier, "Daylight common room and communications", (-23, 23, -59, -35), 5.5,
         "The rounded glazed frontage is a shared room, with distinct reading, notices and communications islands.", 0)
    for index, (x, z, yaw) in enumerate(((-13, -52, 12), (13, -52, -8),
                                         (-10, -39, -7), (10, -39, 6)), 1):
        put(identifier, "Shared table %02d" % index, "KH_RestTable", (x, 5.5, z), yaw)
        put(identifier, "South seat %02d" % index, "KH_Bench", (x, 5.5, z-1.2), yaw)
        put(identifier, "North chair %02d" % index, "KH_CampChair", (x, 5.5, z+1.1), 180+yaw)
        put(identifier, "Individual belongings %02d" % index, "KH_ResidentBundle",
            (x+1.6, 5.5, z+.4), index*19, (.65, .65, .65))
        put(identifier, "Kept rug %02d" % index, "KH_RugBlue" if index % 2 else "KH_RugRed",
            (x, 5.509, z), yaw-5, (1.2, 1, 1.4), "soft floor")
    put(identifier, "Radio and contact desk", "KH_RadioDesk", (20.5, 5.5, -44.5), 90)
    put(identifier, "Contact desk stool", "KH_Stool", (19.2, 5.5, -44.5), 90)
    put(identifier, "Cooperative notices", "KH_CommunityBoard", (-20.5, 5.5, -44.5), -90)
    put(identifier, "Shared reading reserve", "KH_PersonalLocker", (-20.5, 5.5, -41.5), -90)
    put(identifier, "Glazed bay plant west", "KH_PottedPlant", (-18, 5.5, -56), 17)
    put(identifier, "Glazed bay plant east", "KH_PottedPlant", (18, 5.5, -56), -29)
    route(identifier + " main loop", [[0, 5.5, -34], [0, 5.5, -44], [0, 5.5, -57]], 4)
    route(identifier + " west island", [[0, 5.5, -45], [-16, 5.5, -45], [-18, 5.5, -51]], 2)
    route(identifier + " east island", [[0, 5.5, -45], [16, 5.5, -45], [18, 5.5, -51]], 2)
    camera(identifier, (0, 7.15, -40), (13, 6.6, -52), "The unoccupied upper front becomes a shared daylight district")

    for side, label in ((-1, "Mending and quiet work"), (1, "Laundry and household care")):
        identifier = "Upper rear " + label
        district_bounds = [-45.5, -18, 29, 46] if side < 0 else [18, 43, 29, 46]
        zone(identifier, label, district_bounds, 5.5,
             "Quiet work stays behind the rear gallery, with household supplies near their users.")
        put(identifier, "Household work station", "KH_RepairBench" if side < 0 else "KH_WashStation",
            (side*30, 5.5, 35), 180)
        # West household storage sits OUTSIDE the complete U-stair footprint.
        # The left service lane also reaches it without crossing the stair.
        storage_x = -44.5 if side < 0 else 41
        put(identifier, "Household rack", "KH_UtilityRack", (storage_x, 5.5, 32), side*90)
        put(identifier, "Personal storage", "KH_PersonalLocker", (storage_x, 5.5, 41), side*90)
        put(identifier, "Shared work table", "KH_RestTable", (side*22, 5.5, 35), 8*side)
        put(identifier, "South work chair", "KH_CampChair", (side*22, 5.5, 33.8), 0)
        put(identifier, "North work chair", "KH_CampChair", (side*22, 5.5, 36.2), 180)
        put(identifier, "Sorted cloth bundle", "KH_ResidentBundle", (side*29.7, 5.5, 38), 17*side)
        put(identifier, "Household supply cases", "KH_PalletCrates", (side*32, 5.5, 43), 11*side)
        put(identifier, "Workplace rug", "KH_RugWoven", (side*22, 5.508, 35), 8*side,
            (1.2, 1, 1.4), "soft floor")
        route(identifier + " approach", [[side*20, 5.5, 25], [side*20, 5.5, 30],
                                         [side*26, 5.5, 30], [side*34, 5.5, 30],
                                         [side*34, 5.5, 35], [side*37, 5.5, 35]], 2)
        if side < 0:
            route(identifier + " outside stair service alcove",
                  [[-37, 5.5, 35], [-43.1, 5.5, 35], [-43.1, 5.5, 41]], 1.2,
                  "Access the household storage outside the retained complete U-stair footprint")
            camera(identifier + " west stair-side alcove", (-43.1, 7.15, 35),
                   (-44.5, 6.6, 40), "Inspect the usable strip beside the U stair, not inside its solid construction")
        camera(identifier, (side*26, 7.15, 30), (side*34, 6.7, 38), "Rear household function beyond the photographed bunks")

    identifier = "Upper rear community study"
    zone(identifier, "Community council and study", (-10, 12, 48, 63), 5.5,
         "A few tables and an information wall justify the quiet rear floor without replacing it with a junk grid.")
    for index, (x, z) in enumerate(((-6, 52), (6, 52), (-6, 59), (6, 59)), 1):
        put(identifier, "Study table %02d" % index, "KH_RestTable", (x, 5.5, z), index*3-7)
        put(identifier, "Study seat south %02d" % index, "KH_Bench", (x, 5.5, z-1.1))
        put(identifier, "Study seat north %02d" % index, "KH_CampChair", (x, 5.5, z+1.1), 180)
    put(identifier, "Council notices", "KH_CommunityBoard", (-3.5, 5.5, 62.5))
    put(identifier, "Study locker", "KH_PersonalLocker", (10.5, 5.5, 59), 90)
    route(identifier + " centre", [[0, 5.5, 47], [0, 5.5, 55], [0, 5.5, 61]], 3)
    camera(identifier, (0, 7.15, 49), (6, 6.7, 58), "Far rear usable floor and its activity islands")

    for side, label, x in ((-1, "Caretaker strip", -41), (1, "Household reserve", 40.5)):
        identifier = "Upper far rear " + label
        bounds = (-44, -31, 48, 63) if side < 0 else (18, 44, 48, 63)
        zone(identifier, label, bounds, 5.5,
             "Usable rear edges have service functions; the sealed blue bulkhead is explicitly excluded.")
        put(identifier, "Repair station" if side < 0 else "Laundry wash point",
            "KH_RepairBench" if side < 0 else "KH_WashStation", (x, 5.5, 52), side*90)
        put(identifier, "Reserve rack", "KH_UtilityRack", (x, 5.5, 58), side*90)
        put(identifier, "Personal service locker", "KH_PersonalLocker", (x, 5.5, 62), side*90)
        put(identifier, "Stored household cases", "KH_PalletCrates", (x, 5.5, 60.4), 13*side)
        put(identifier, "Working stool", "KH_Stool", (x-side*1.4, 5.5, 52), side*90)
        if side > 0:
            put(identifier, "Filtered household water", "KH_WaterTreatment", (25, 5.5, 60), 0)
            put(identifier, "Cloth sorting table", "KH_RestTable", (25, 5.5, 52), 9)
            put(identifier, "Sorted dry linen", "KH_SupplySackStack", (29, 5.5, 61), -17)
        route(identifier + " service walk", [[x-side*3, 5.5, 48], [x-side*3, 5.5, 55],
                                             [x-side*3, 5.5, 61]], 1.8)
        camera(identifier, (x-side*3, 7.15, 54), (x, 6.7, 59), "Far rear corner and wall edge")

    # A service walk is not a furniture corridor.  Small edge alcoves leave
    # X=±42.4 open, and the cross-gaps at Z=-14/+2 still link back to the arcade.
    for y, floor in ((0, "Ground"), (5.5, "Upper")):
        for side, label in ((-1, "West"), (1, "East")):
            identifier = floor + " outer walk " + label
            start_z = -27 if y == 5.5 and side < 0 else -30
            zone(identifier, "Service walk and repair alcoves", sorted((side*40.3, side*45.7))+[start_z, 20], y,
                 "A continuous route is maintained; wall-side maintenance alcoves explain the quieter outer perimeter.", 2)
            for index, z in enumerate((-22, -6, 10), 1):
                prop = "KH_PowerSwitchboard" if index == 1 else "KH_PersonalLocker" if index == 2 else "KH_UtilityRack"
                put(identifier, "Wall service alcove %02d" % index, prop, (side*45, y, z), side*90)
                put(identifier, "Alcove kept case %02d" % index, "KH_PalletCrates",
                    (side*45, y, z+2.3), side*90, (.65, .65, .65))
            route(identifier + " continuous walk", [[side*42.4, y, start_z], [side*42.4, y, -14],
                                                    [side*42.4, y, 2], [side*42.4, y, 19]], 2)
            camera(identifier, (side*42.4, y+1.65, -14), (side*45, y+1, -5), "Outer route stays clear beside purposeful edge alcoves")

    identifier = "Roof west service"
    zone(identifier, "Roof maintenance and reserve power", (-42, -32, 14, 29), 11,
         "Reachable Y11 roof space serves maintenance; the sealed advertising caps are not counted as playable rooms.", 2)
    put(identifier, "Open air reserve generator", "KH_DieselGenerator", (-40.5, 11, 18), -90)
    put(identifier, "Roof service distribution", "KH_PowerSwitchboard", (-40.5, 11, 23), -90)
    put(identifier, "Roof repair bench", "KH_RepairBench", (-33.5, 11, 19), 90)
    put(identifier, "Maintenance reserve", "KH_PalletCrates", (-33.5, 11, 23), -7)
    put(identifier, "Service worker stool", "KH_Stool", (-35, 11, 19), 90)
    route(identifier + " approach", [[-36.5, 11, 36.3], [-36.5, 11, 27],
                                     [-36.5, 11, 19], [-38.5, 11, 18]], 2)
    camera(identifier, (-36.5, 12.65, 26), (-39.5, 11.8, 19), "Reachable maintenance equipment and unobstructed roof-stair route")

    identifier = "Roof east observation support"
    zone(identifier, "Observation and radio support", (33, 43, -10, 5), 11,
         "Guard rest and communications support the existing reachable east lookout, away from the advertising shoulder.", 2)
    put(identifier, "Observation radio", "KH_RadioDesk", (40.5, 11, -3), 90)
    put(identifier, "Duty stool", "KH_Stool", (39.1, 11, -3), 90)
    put(identifier, "Duty locker", "KH_PersonalLocker", (40.5, 11, 1), 90)
    put(identifier, "Watch reserve", "KH_PalletCrates", (33.9, 11, 1), 14)
    put(identifier, "Rest seat", "KH_CampChair", (34.5, 11, -3), 13)
    route(identifier + " reachable lookout", [[36, 11, 26], [36, 11, 7],
                                             [36, 11, 1], [36, 11, -12],
                                             [36, 11, -19]], 2)
    camera(identifier, (36, 12.65, 3), (40.5, 11.9, -3), "Reachable observation support and radio equipment")

    # Complete the inspection context and give every deliberate open area a
    # concrete role.  Sealed cavities are not passed off as inspected interiors.
    for y in (0, 5.5):
        for side, label in ((-1, "West"), (1, "East")):
            box(label + " public arcade Y" + str(y), sorted((side*22, side*24))+[-34, 23], y,
                "Two-way circulation, suite entrances and existing columns; no new furniture")
            start_z = -27 if y == 5.5 and side < 0 else -31
            box(label + " outer service route Y" + str(y), sorted((side*41.4, side*43.4))+[start_z, 23], y,
                "Continuous outer maintenance walk and access to cross-gaps")
        box("Rear transverse route Y" + str(y), [-44, 44, 23, 25.5], y,
            "Public crossing and delivery route behind both ordinary stairs")
        box("Front central route Y" + str(y), [-3, 3, -62, -40], y,
            "Front approach or upper common-room circulation and view axis")
        box("Rear central route Y" + str(y), [-2, 2, 47, 63], y,
            "Meal/council access, carrying supplies and assembly space")
    box("Market public through lane", [2.2, 4.6, -31, 8], 0,
        "Retained tested market lane around the communal hearth and traders")
    for x, label in ((-10, "West"), (10, "East")):
        box(label + " stair lower approach", [x-2.25, x+2.25, 5.5, 8.1], 0,
            "Clear entrance to ordinary stair")
        box(label + " stair upper landing", [x-2.25, x+2.25, 17.9, 22.5], 5.5,
            "Clear exit and return onto the upper rear gallery")
    box("Upper roof stair approach", [-41, -38, 27, 38], 5.5,
        "Preserved approach to the first flight of the U stair")
    box("Upper complete roof stair footprint", [-42.4, -33.6, 38, 46], 5.5,
        "Retained solid U stair, turn, guard rails and access to both flights")
    box("Roof stair door and exit", [-38.5, -34.5, 33.5, 38], 11,
        "Protected exit from the roof-access chamber")
    box("Roof west service approach", [-37.5, -35.5, 26, 36.3], 11,
        "Connection from roof stair to west service equipment and cross-route")
    for identifier, bounds, reason in (
            ("Sealed upper west advertising shoulder", [-44, -30, -49.7, -28],
             "Existing closed advertising volume starts at Y5.5; no doorway"),
            ("Sealed upper east advertising shoulder", [30, 45, -49.7, -35],
             "Existing closed advertising volume starts at Y5.5; no doorway"),
            ("Sealed upper rear blue bulkhead", [-28, -12, 49, 64.6],
             "Existing closed service bulkhead; not an accessible empty room")):
        exclusions.append(dict(id=identifier, type="inaccessible retained volume", floorY=5.5,
                               bounds=bounds, reason=reason))
    exclusions.extend([
        dict(id="Under ordinary stairs", type="solid retained construction", floorY=0,
             footprints=[[-12, -8, 8, 18], [8, 12, 8, 18]],
             reason="Stepped concrete blocks fill the volume; no occupiable understair cavity"),
        dict(id="West high lookout", type="no normal access", floorY=14.1,
             bounds=[-44, -30, -49.7, -28],
             reason="Existing high watchpost has no normal stair connection; do not include in the walk tour"),
        dict(id="Roof skylights and maintenance", type="open service and safety area", floorY=11,
             reason="Keep 2 m service clearance around guarded central skylight and dome; no new junk grid"),
    ])
    camera("Arrival full axis", (0, 1.65, -57), (0, 2, -34.5), "Entry, checkpoint and layered atrium beyond")
    camera("Market and community hearth", (3.4, 1.65, -28), (0, 1.2, -14), "Preserved main market lane")
    camera("Upper rear gallery", (0, 7.15, 24), (0, 6.8, -8), "Rear gallery, banner and existing evergreen")
    for side, label in ((-1, "West"), (1, "East")):
        camera(label + " sleeping front", (side*16.9, 7.15, -24), (side*18.6, 6.6, -17),
               "Near-rail density, privacy cloth and personal belongings")
        camera(label + " sleeping rear alley", (side*23, 7.15, -25), (side*25.2, 6.4, -15),
               "Occupied back lane beyond the sleeping fronts")
    story.extend([
        dict(layer="Before catastrophe", evidence="Retained two-storey mall shell, glazed storefront modules, ceramic floor and advertising shoulders"),
        dict(layer="Failure and exposure", evidence="Retained repairs, local damp/wear and shuttered inaccessible advertising/service volumes; not every room equally dirty"),
        dict(layer="Adaptation", evidence="Workshop fronts, cable mounts on existing walls, purpose-built water/electrical service equipment and domestic wash points"),
        dict(layer="Current life", evidence="Rations, maintained clinical rooms, personal lockers, quiet work, communal meals, family sleeping niches and staff rest"),
    ])
    for row in zones:
        row["placementIds"] = [i for i in additions if i.startswith("Hub8 " + row["id"] + " ")]
        # Inspection samples are FLOOR positions copied from this district's
        # declared route waypoints, never furniture-prone region centres or
        # eye-height camera poses.  Distinct samples spread along that route.
        x0, x1, z0, z1 = row["bounds"]
        prefix = "Hub8 " + row["id"] + " "
        candidates, seen = [], set()
        for access in routes:
            if not access["id"].startswith(prefix):
                continue
            for index, point in enumerate(access["points"]):
                key = tuple(point)
                if (key in seen or abs(point[1]-row["floorY"]) > .00001
                        or not x0 <= point[0] <= x1 or not z0 <= point[2] <= z1):
                    continue
                seen.add(key)
                candidates.append(dict(point=list(point), routeId=access["id"],
                                       waypointIndex=index, distanceToRoute=0.0))
        if not candidates:
            raise AssertionError("No real in-bounds route waypoint for district: " + row["id"])
        chosen = [candidates.pop(0)]
        while candidates and len(chosen) < 3:
            next_point = max(candidates, key=lambda item: min(
                (item["point"][0]-picked["point"][0])**2
                + (item["point"][2]-picked["point"][2])**2 for picked in chosen))
            chosen.append(next_point)
            candidates.remove(next_point)
        row["inspectionPoints"] = [sample["point"] for sample in chosen]
        row["inspectionAccess"] = chosen
        row["inspectionValidation"] = "Route-authored floor samples; real collider/NavMesh reachability must still be verified"
    # Check only new full-size furniture against protected rectangles.  Small
    # surface stock, soft floor coverings and wall-mounted fixtures intentionally
    # share support surfaces and are checked by the imported-scene audit.
    envelope_conflicts = []
    for identifier, a in furniture:
        for clear in exclusions:
            if clear["type"] != "clear footprint" or abs(clear["floorY"]-a[4]) > .1:
                continue
            b = clear["bounds"]
            dx, dz = min(a[1], b[1])-max(a[0], b[0]), min(a[3], b[3])-max(a[2], b[2])
            if dx > .01 and dz > .01:
                envelope_conflicts.append(dict(placement=identifier, exclusion=clear["id"],
                                               overlap=[round(dx, 4), round(dz, 4)]))
    for key, old in old_groups.items():
        current = manifest.get(key, [])
        if current[:len(old)] != old:
            raise AssertionError("Existing placement group changed: " + key)
    if len(additions) != len(set(additions)):
        raise AssertionError("Duplicate Hub8 stable identifiers")
    result = dict(revision=8, coordinates="Unity local metres; X right/Y up/front -Z",
                  zones=zones, routes=routes, cameras=cameras,
                  circulationExclusions=exclusions, story=story,
                  addedPlacementIds=additions, addedPlacementCount=len(additions),
                  placementRoles=roles, conservativePropEnvelopes=PROP_ENVELOPES,
                  conservativeEnvelopeConflicts=envelope_conflicts,
                  lighting="No Light data added. Root power-network/worklamp pass must connect visible fixtures and assign their actual lights.",
                  validation="Spatial authoring data; imported geometry/colliders and player-height Play traversal still required")
    manifest["hub8Spatial"] = result
    return result
