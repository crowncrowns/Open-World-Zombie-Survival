"""Constructed domestic and medical props for the Kurs hub quality pass.

Builder coordinates are metres, Unity +Y up, and the presentation front is -Z.
This module has no Blender dependency and writes no files itself.  All graphics
are deliberately small surface labels; category names do not replace equipment.
"""

import math


def build(api):
    def box(name, p, s, mat="Frame", bevel=.006, collision=True, yaw=0):
        api.box(name, p, s, mat=mat, bevel=bevel,
                collision=collision, yaw=yaw)

    def pipe(name, points, radius=.02, mat="Frame", sides=None,
             collision=False):
        api.pipe(name, points, radius=radius, mat=mat,
                 sides=sides or api.N(16, 10, 6), collision=collision)

    def ring(name, p, radius, tube=.009, mat="Frame", plane="xz"):
        count = api.N(36, 22, 12)
        points = []
        for i in range(count + 1):
            a = 2 * math.pi * i / count
            d = (radius * math.cos(a), radius * math.sin(a))
            if plane == "yz":
                points.append((p[0], p[1] + d[0], p[2] + d[1]))
            else:
                points.append((p[0] + d[0], p[1], p[2] + d[1]))
        pipe(name, points, tube, mat, api.N(12, 8, 6))

    def lathe(name, p, profile, mat="Enamel", collision=True,
              sides=None):
        api.lathe(name, p, profile=profile, mat=mat,
                  sides=sides or api.N(40, 24, 16), collision=collision)

    def label(name, words, p, size=.032, mat="Lettering", yaw=0):
        api.text(name, words, p, size=size, mat=mat, yaw=yaw)

    def bolt(name, x, y, z, radius=.007):
        pipe(name, [(x, y, z + .004), (x, y, z - .004)], radius,
             "Frame", api.N(8, 6, 6))

    def bottle(name, x, y, z, h=.18, radius=.035, mat="Glass"):
        lathe(name + "_Body", (x, y, z),
              [(0, radius * .78), (.009, radius),
               (h * .66, radius), (h * .79, radius * .53),
               (h * .93, radius * .45), (h, radius * .45)], mat,
              collision=False, sides=api.N(24, 16, 10))
        lathe(name + "_Cap", (x, y + h * .93, z),
              [(0, radius * .49), (h * .10, radius * .49)],
              "PlasticDark", collision=False, sides=api.N(20, 12, 8))
        if api.N(1, 1, 0):
            box(name + "_PaperLabel", (x, y + h * .43, z - radius - .001),
                (radius * 1.4, h * .26, .0015), "Paper", .0005, False)

    def wheel(name, x, y, z):
        # The wheel lies in YZ, with its axle across X.  A horizontal lathe
        # washer would look incorrect on a caster, so use a real tyre section.
        ring(name + "_Tyre", (x, y, z), .043, .017,
             "PlasticDark", "yz")
        pipe(name + "_Hub", [(x - .022, y, z), (x + .022, y, z)],
             .025, "Frame", api.N(20, 12, 8))
        for side in (-1, 1):
            box(name + "_Fork_" + str(side),
                (x + side * .025, y + .035, z), (.009, .075, .036),
                "Frame", .004)
        lathe(name + "_Swivel", (x, y + .075, z),
              [(0, .035), (.016, .035), (.021, .023), (.047, .023)],
              "Frame", sides=api.N(20, 12, 8))

    def board_seam(name, left, right, y, z, mat="InkDark"):
        # Ink has width in the paper plane, not the round profile of a wire.
        box(name, ((left + right) * .5, y, z),
            (right - left, .0018, .00025), mat, .00005, False)

    # Medical cot: structural tubes, articulated channelled back, mattress
    # layers, seam piping and caster forks remain separate authored pieces.
    api.begin("KH_MedicalCot",
              "Tubular medical cot with articulated back, layered upholstery, "
              "tied blanket, four swivel casters and side adjustment knobs")
    for side in (-1, 1):
        x = side * .43
        pipe("Cot_LongRail_" + str(side),
             [(x, .46, -1.03), (x, .46, 1.03)], .029)
        pipe("Cot_LowerBrace_" + str(side),
             [(x, .23, -.81), (x, .23, .81)], .019)
        pipe("Cot_SafetyRail_" + str(side),
             [(x, .52, -.56), (x, .78, -.56),
              (x, .81, -.49), (x, .81, .26), (x, .73, .32)], .017)
        for end in (-1, 1):
            z = end * .84
            pipe("Cot_Leg_%s_%s" % (side, end),
                 [(x, .19, z), (x, .49, z)], .026)
            wheel("Cot_Caster_%s_%s" % (side, end), x, .063, z)
        pipe("Cot_BackHinge_" + str(side),
             [(x - .025, .59, .32), (x + .025, .59, .32)],
             .049, "PlasticDark", api.N(24, 16, 10))
        pipe("Cot_AdjustmentKnob_" + str(side),
             [(x, .59, .32), (x + side * .075, .59, .32)],
             .027, "Copper", api.N(20, 12, 8))
    for end in (-1, 1):
        z = end * 1.025
        height = .99 if end > 0 else .75
        pipe("Cot_EndBow_" + str(end),
             [(-.43, .46, z), (-.43, height - .065, z),
              (-.37, height, z), (.37, height, z),
              (.43, height - .065, z), (.43, .46, z)], .024)
        pipe("Cot_EndCross_" + str(end),
             [(-.42, height - .18, z), (.42, height - .18, z)], .015)
    for i in range(api.N(9, 6, 4)):
        z = -.92 + i * 1.23 / max(1, api.N(9, 6, 4) - 1)
        box("Cot_DeckSlat_%02d" % i, (0, .485, z), (.80, .018, .075),
            "WornWood", .003)
    box("Cot_MattressBase", (0, .552, -.32), (.80, .105, 1.37),
        "CanvasBeige", .034)
    box("Cot_MattressTop", (0, .610, -.32), (.77, .052, 1.34),
        "CanvasBeige", .024)
    pipe("Cot_MattressEdgePiping",
         [(-.36, .626, -.96), (.36, .626, -.96), (.385, .626, -.93),
          (.385, .626, .30), (.36, .626, .335), (-.36, .626, .335),
          (-.385, .626, .30), (-.385, .626, -.93), (-.36, .626, -.96)],
         .004, "CanvasBlue", api.N(8, 6, 4))
    for side in (-1, 1):
        pipe("Cot_RaisedBackRail_" + str(side),
             [(side * .38, .53, .30), (side * .38, .83, .99)], .021)
    sections = api.N(8, 6, 4)
    for i in range(sections):
        t = (i + .5) / sections
        box("Cot_BackFoamChannel_%02d" % i,
            (0, .615 + .29 * t, .335 + .64 * t),
            (.77, .098, .65 / sections + .014), "CanvasBeige", .025)
    box("Cot_Pillow", (0, .930, .845), (.53, .14, .27),
        "CanvasBeige", .059)
    box("Cot_PillowFold", (.01, .971, .835), (.48, .032, .24),
        "CanvasBeige", .014, False)
    box("Cot_FootBlanket", (0, .675, -.62), (.79, .055, .62),
        "CanvasBlue", .018)
    for side in (-1, 1):
        box("Cot_BlanketDrape_" + str(side),
            (side * .393, .607, -.62), (.025, .15, .62),
            "CanvasBlue", .01, False)
        pipe("Cot_BlanketTie_" + str(side),
             [(side * .37, .69, -.76), (side * .45, .64, -.76),
              (side * .45, .53, -.76), (side * .43, .50, -.74)],
             .003, "SandCanvas", api.N(8, 6, 4))
    if api.N(1, 1, 0):
        for i in range(api.N(12, 8, 0)):
            x = -.34 + i * .68 / max(1, api.N(12, 8, 0) - 1)
            pipe("Cot_BlanketHem_%02d" % i,
                 [(x, .705, -.903), (x + .014, .705, -.888)],
                 .0015, "CanvasBeige", 6)
    label("Cot_InventoryTag", "МП-04", (-.12, .66, -1.052), .032,
          "InkDark")
    api.end()

    # Medical cabinet: openable frame construction; one glass leaf is ajar.
    api.begin("KH_MedicalCabinet",
              "Enamel medical cupboard, glazed doors, formed shelves, "
              "drawer, hinges, bottles and marked first-aid case")
    for x in (-.565, .565):
        for z in (-.17, .17):
            box("Cabinet_Foot_%s_%s" % (x, z), (x, .105, z),
                (.045, .21, .045), "Frame", .005)
    box("Cabinet_Base", (0, .245, 0), (1.25, .075, .45), "Enamel", .012)
    box("Cabinet_Back", (0, 1.015, .214), (1.22, 1.48, .024),
        "Enamel", .005)
    for side in (-1, 1):
        box("Cabinet_Side_" + str(side), (side * .61, 1.035, 0),
            (.027, 1.48, .45), "Enamel", .008)
    box("Cabinet_Crown", (0, 1.785, 0), (1.25, .030, .45),
        "Enamel", .009)
    for index, y in enumerate((.62, .99, 1.36)):
        box("Cabinet_Shelf_%d" % index, (0, y, -.005),
            (1.18, .018, .39), "Frame", .004)
        box("Cabinet_ShelfLip_%d" % index, (0, y + .018, -.198),
            (1.18, .034, .012), "Enamel", .003)
    box("Cabinet_DrawerFront", (0, .454, -.231), (1.17, .23, .027),
        "Enamel", .008)
    pipe("Cabinet_DrawerPull",
         [(-.13, .46, -.249), (-.13, .46, -.288),
          (.13, .46, -.288), (.13, .46, -.249)], .011)
    for side in (-1, 1):
        angle = 24 if side < 0 else 0
        hinge_x = side * .588
        sign = -side
        a = math.radians(angle)

        def dp(local_x, y, local_z=0):
            return (hinge_x + local_x * math.cos(a) + local_z * math.sin(a),
                    y, -.237 - local_x * math.sin(a) + local_z * math.cos(a))

        center = dp(sign * .282, 1.168)
        box("Cabinet_Glass_" + str(side), center, (.515, 1.065, .008),
            "Glass", .002, False, angle)
        for rail in (-1, 1):
            box("Cabinet_DoorVertical_%s_%s" % (side, rail),
                dp(sign * (.282 + rail * .269), 1.168),
                (.023, 1.16, .026), "Enamel", .005, True, angle)
            box("Cabinet_DoorHorizontal_%s_%s" % (side, rail),
                dp(sign * .282, 1.168 + rail * .568),
                (.56, .024, .026), "Enamel", .005, True, angle)
        pull_x = sign * .502
        pipe("Cabinet_DoorPull_" + str(side),
             [dp(pull_x, 1.03, -.015), dp(pull_x, 1.03, -.048),
              dp(pull_x, 1.23, -.048), dp(pull_x, 1.23, -.015)], .008)
        for y in (.76, 1.57):
            lathe("Cabinet_Hinge_%s_%s" % (side, y),
                  (hinge_x, y - .035, -.225),
                  [(0, .015), (.070, .015)], "Frame",
                  sides=api.N(16, 10, 6))
    for row, y in enumerate((.648, 1.018, 1.388)):
        count = api.N(8, 6, 4)
        for i in range(count):
            x = -.48 + i * .96 / (count - 1)
            h = (.17, .21, .14, .18)[i % 4]
            bottle("Cabinet_Bottle_%s_%s" % (row, i), x, y, .06,
                   h, .026 + .004 * (i % 2),
                   "Glass" if (i + row) % 3 else "PlasticDark")
    box("Cabinet_FirstAidCase", (.21, .755, -.10), (.34, .20, .22),
        "CanvasBeige", .018)
    box("Cabinet_CrossVertical", (.21, .755, -.214), (.026, .112, .002),
        "SignRed", .001, False)
    box("Cabinet_CrossHorizontal", (.21, .755, -.215), (.10, .027, .002),
        "SignRed", .001, False)
    pipe("Cabinet_CaseHandle",
         [(.15, .864, -.10), (.15, .895, -.10),
          (.27, .895, -.10), (.27, .864, -.10)], .008,
         "PlasticDark")
    label("Cabinet_ShelfTag", "ПЕРЕВЯЗОЧНОЕ", (-.23, .464, -.247), .027,
          "InkDark")
    api.end()

    # Kitchen range: independent load-bearing legs and open cylinder bay,
    # formed burner rings, grate bars and vessels with real wall profiles.
    api.begin("KH_KitchenRange",
              "Repaired community range with four ring burners, oven, "
              "stockpots, open fuel-cylinder bay and connected manifold")
    for x in (-1.035, .55, 1.035):
        for z in (-.345, .345):
            box("Range_Leg_%s_%s" % (x, z), (x, .15, z),
                (.055, .30, .055), "Frame", .005)
            box("Range_Foot_%s_%s" % (x, z), (x, .025, z),
                (.095, .05, .095), "PlasticDark", .009)
    box("Range_OvenBody", (-.25, .535, 0), (1.70, .66, .78),
        "Enamel", .017)
    box("Range_Hob", (0, .89, 0), (2.20, .060, .85), "Frame", .011)
    box("Range_FrontRail", (0, .826, -.413), (2.17, .082, .026),
        "Enamel", .008)
    box("Range_BackSplash", (0, 1.075, .398), (2.18, .31, .025),
        "Enamel", .009)
    for x in (-1.01, 1.01):
        pipe("Range_HandrailPost_" + str(x),
             [(x, .91, .36), (x, 1.30, .36)], .018)
    pipe("Range_BackHandrail", [(-1.01, 1.30, .36), (1.01, 1.30, .36)],
         .018)
    for j, z in enumerate((-.21, .21)):
        for i, x in enumerate((-.58, .32)):
            lathe("Range_BurnerBase_%s_%s" % (i, j), (x, .92, z),
                  [(0, .13), (.014, .13), (.025, .105), (.031, .105)],
                  "PlasticDark", sides=api.N(32, 20, 12))
            ring("Range_BurnerRing_%s_%s" % (i, j), (x, .956, z),
                 .106, .011, "SteelOxide")
            for axis in (-1, 1):
                pipe("Range_GrateX_%s_%s_%s" % (i, j, axis),
                     [(x - .17, .976, z + axis * .052),
                      (x + .17, .976, z + axis * .052)],
                     .012, "SteelOxide")
                pipe("Range_GrateZ_%s_%s_%s" % (i, j, axis),
                     [(x + axis * .052, .976, z - .17),
                      (x + axis * .052, .976, z + .17)],
                     .012, "SteelOxide")
    box("Range_OvenDoor", (-.50, .48, -.407), (1.08, .46, .035),
        "SteelOxide", .014)
    box("Range_OvenWindowRim", (-.50, .445, -.430), (.77, .25, .014),
        "PlasticDark", .012)
    box("Range_OvenGlass", (-.50, .445, -.440), (.71, .20, .008),
        "Glass", .009, False)
    pipe("Range_OvenPull",
         [(-.88, .628, -.430), (-.88, .628, -.492),
          (-.12, .628, -.492), (-.12, .628, -.430)],
         .016, "Frame")
    for x in (-.98, -.02):
        box("Range_DoorHinge_" + str(x), (x, .27, -.434),
            (.09, .035, .035), "Frame", .005)
    box("Range_ServiceDoor", (.335, .48, -.407), (.43, .46, .029),
        "Enamel", .009)
    pipe("Range_ServicePull",
         [(.335, .55, -.43), (.335, .55, -.468),
          (.335, .42, -.468), (.335, .42, -.43)], .009)
    for i, x in enumerate((-.76, -.48, -.20, .08)):
        pipe("Range_Control_%d" % i,
             [(x, .83, -.433), (x, .83, -.470)],
             .027, "PlasticDark", api.N(20, 12, 8))
        box("Range_ControlMark_%d" % i, (x, .844, -.473),
            (.004, .014, .002), "Paper", .0005, False)
    lathe("Range_FuelCylinder", (.845, .10, .045),
          [(0, .105), (.022, .139), (.08, .151), (.50, .151),
           (.563, .126), (.60, .068), (.628, .047)],
          "PlasticDark", sides=api.N(40, 24, 16))
    for y in (.19, .54):
        ring("Range_FuelStrap_" + str(y), (.845, y, .045),
             .153, .009, "Frame")
    lathe("Range_CylinderValve", (.845, .728, .045),
          [(0, .022), (.040, .022)], "Copper",
          sides=api.N(16, 10, 8))
    ring("Range_ValveWheel", (.845, .775, .045), .039, .006, "SignRed")
    pipe("Range_FuelHose",
         [(.845, .748, .045), (.98, .75, .07), (1.01, .77, .27),
          (.96, .815, .34), (.38, .815, .34), (-.58, .815, .34)],
         .012, "PlasticDark")
    for x in (-.58, .32):
        pipe("Range_Manifold_" + str(x),
             [(x, .815, .34), (x, .875, .21)], .011, "Copper")
    for i, (x, z, radius, height) in enumerate((
            (-.58, -.21, .162, .285), (.32, .21, .142, .235))):
        lathe("Range_Stockpot_%d" % i, (x, .986, z),
              [(0, radius * .86), (.013, radius), (.026, radius),
               (height - .014, radius), (height, radius * .98),
               (height, radius * .91), (.027, radius * .91),
               (.018, radius * .82)], "Enamel",
              sides=api.N(48, 28, 18))
        ring("Range_PotRim_%d" % i, (x, .986 + height, z),
             radius * .97, .008, "Frame")
        for side in (-1, 1):
            pipe("Range_PotHandle_%s_%s" % (i, side),
                 [(x + side * radius, 1.00 + height * .63, z - .055),
                  (x + side * (radius + .072), 1.00 + height * .63, z - .055),
                  (x + side * (radius + .072), 1.00 + height * .63, z + .055),
                  (x + side * radius, 1.00 + height * .63, z + .055)],
                 .010, "PlasticDark")
        if i == 0:
            lathe("Range_PotLid", (x, .986 + height + .009, z),
                  [(0, radius * 1.03), (.009, radius * 1.03),
                   (.025, radius * .82), (.038, radius * .18)],
                  "Frame", sides=api.N(48, 28, 18))
            pipe("Range_LidGrip",
                 [(x - .045, 1.044 + height, z),
                  (x - .045, 1.073 + height, z),
                  (x + .045, 1.073 + height, z),
                  (x + .045, 1.044 + height, z)], .010, "PlasticDark")
    label("Range_RepairPlate", "КУХНЯ / ГАЗ", (.62, .83, -.435), .027,
          "InkDark")
    api.end()

    # Wash stand: a genuinely concave pressed bowl, open underframe,
    # connected reservoir, hand pump, mixer neck and waste trap.
    api.begin("KH_WashStation",
              "Community wash stand with pressed basin, hand pump, gravity "
              "reservoir, connected waste trap, soap and draped hand towel")
    for x in (-.83, .83):
        for z in (-.29, .29):
            pipe("Wash_Leg_%s_%s" % (x, z),
                 [(x, .026, z), (x, .87, z)], .025)
            box("Wash_Foot_%s_%s" % (x, z), (x, .025, z),
                (.08, .05, .08), "PlasticDark", .008)
    for z in (-.31, .31):
        pipe("Wash_TopRail_" + str(z),
             [(-.87, .875, z), (.87, .875, z)], .023)
        pipe("Wash_LowerRail_" + str(z),
             [(-.83, .27, z), (.83, .27, z)], .018)
    box("Wash_RightWorktop", (.49, .87, 0), (.78, .034, .69),
        "Enamel", .013)
    box("Wash_LeftRim", (-.83, .87, 0), (.14, .034, .69),
        "Enamel", .009)
    for z in (-.307, .307):
        box("Wash_BasinEdge_" + str(z), (-.36, .87, z),
            (.82, .034, .078), "Enamel", .009)
    lathe("Wash_PressedBasin", (-.36, .697, 0),
          [(0, .055), (.014, .13), (.06, .22), (.155, .288),
           (.176, .312), (.185, .312), (.185, .290),
           (.159, .278), (.057, .207), (.022, .125), (.017, .055)],
          "Frame", sides=api.N(56, 32, 20))
    ring("Wash_RolledBasinRim", (-.36, .881, 0), .302, .008, "Frame")
    lathe("Wash_DrainGrille", (-.36, .716, 0),
          [(0, .039), (.006, .039)], "PlasticDark",
          collision=False, sides=api.N(24, 16, 10))
    if api.N(1, 1, 0):
        for dx in (-.018, 0, .018):
            pipe("Wash_DrainSlot_" + str(dx),
                 [(-.36 + dx, .723, -.023), (-.36 + dx, .723, .023)],
                 .002, "Frame", 6)
    pipe("Wash_WasteTrap",
         [(-.36, .703, 0), (-.36, .49, 0), (-.36, .43, .06),
          (-.36, .43, .14), (-.36, .49, .18), (-.36, .57, .18),
          (-.36, .57, .31), (-.36, .18, .31)], .022, "PlasticDark")
    for y in (.63, .51):
        lathe("Wash_TrapUnion_" + str(y), (-.36, y, 0),
              [(0, .031), (.025, .031)], "Frame",
              sides=api.N(16, 10, 8))
    lathe("Wash_Reservoir", (.57, .888, .07),
          [(0, .13), (.016, .157), (.050, .17), (.45, .17),
           (.49, .15), (.512, .10)], "Enamel",
          sides=api.N(40, 24, 16))
    lathe("Wash_ReservoirLid", (.57, 1.402, .07),
          [(0, .105), (.025, .105), (.034, .09)], "Frame",
          sides=api.N(32, 20, 12))
    pipe("Wash_ReservoirHandle",
         [(.51, 1.438, .07), (.51, 1.486, .07),
          (.63, 1.486, .07), (.63, 1.438, .07)], .010)
    lathe("Wash_HandPump", (.04, .891, .20),
          [(0, .072), (.018, .072), (.032, .043), (.18, .043),
           (.205, .052), (.233, .052)], "Frame",
          sides=api.N(32, 20, 12))
    pipe("Wash_PumpLever",
         [(.04, 1.115, .20), (.06, 1.18, .20),
          (.25, 1.33, .20), (.37, 1.33, .20)], .014, "Frame")
    pipe("Wash_PumpGrip", [(.28, 1.33, .20), (.38, 1.33, .20)],
         .023, "TimberRaw")
    pipe("Wash_ReservoirFeed",
         [(.57, .932, -.09), (.37, .932, -.09),
          (.20, .932, .16), (.08, .932, .20)], .011, "Copper")
    pipe("Wash_Spout",
         [(.04, 1.07, .20), (-.04, 1.15, .20),
          (-.18, 1.15, .15), (-.28, 1.10, .05),
          (-.28, 1.035, .05)], .013, "Frame")
    lathe("Wash_SpoutCollar", (-.28, 1.031, .05),
          [(0, .019), (.025, .019)], "Frame",
          sides=api.N(16, 10, 8))
    box("Wash_SoapDish", (.43, .904, -.19), (.19, .025, .115),
        "Frame", .012)
    box("Wash_UsedSoap", (.43, .932, -.19), (.13, .035, .077),
        "CanvasBeige", .017, False)
    pipe("Wash_TowelRail",
         [(-.79, .64, -.315), (-.79, .64, -.389),
          (.12, .64, -.389), (.12, .64, -.315)], .012)
    strips = api.N(9, 6, 4)
    for i in range(strips):
        x = -.62 + .29 * (i + .5) / strips
        fold = math.sin(i * math.pi * 1.21) * .009
        box("Wash_TowelFront_%d" % i, (x, .468, -.408 + fold),
            (.29 / strips + .002, .344, .005), "CanvasBlue", .002, False)
        box("Wash_TowelBack_%d" % i, (x, .51, -.373 + fold),
            (.29 / strips + .002, .26, .005), "CanvasBlue", .002, False)
        pipe("Wash_TowelFold_%d" % i,
             [(x, .625, -.373 + fold), (x, .649, -.386 + fold),
              (x, .636, -.407 + fold)],
             .004, "CanvasBlue", api.N(8, 6, 4))
    label("Wash_WaterTag", "ВОДА / РУКИ", (.39, 1.15, -.105), .025,
          "InkDark")
    api.end()

    # Different repairs and personal ownership distinguish the three lockers.
    api.begin("KH_PersonalLocker",
              "Three reused sheet-metal lockers with distinct doors, "
              "vent hoods, hinges, padlock, name cards and a personal picture")
    box("Locker_Plinth", (0, .075, 0), (1.50, .15, .50),
        "SteelOxide", .01)
    box("Locker_Back", (0, 1.02, .239), (1.48, 1.74, .022),
        "Enamel", .004)
    for x in (-.738, -.25, .25, .738):
        box("Locker_Partition_" + str(x), (x, 1.02, 0),
            (.022, 1.74, .50), "Enamel", .004)
    box("Locker_Crown", (0, 1.885, 0), (1.50, .03, .50),
        "Enamel", .008)
    box("Locker_BaseShelf", (0, .169, 0), (1.47, .025, .46),
        "Frame", .003)
    names = ("АННА", "МИША", "СЕМЬЯ 07")
    door_mats = ("Enamel", "SteelOxide", "PlasticDark")
    for i, x in enumerate((-.50, 0, .50)):
        mat = door_mats[i]
        box("Locker_Door_%d" % i, (x, 1.02, -.258),
            (.465, 1.68, .025), mat, .008)
        box("Locker_NameCard_%d" % i, (x, 1.60, -.2725),
            (.24, .085, .002), "Paper", .001, False)
        label("Locker_Name_%d" % i, names[i],
              (x - .103, 1.59, -.2745), .025, "InkDark")
        vents = api.N(7, 5, 3)
        for j in range(vents):
            y = 1.30 + j * .035
            box("Locker_VentShadow_%s_%s" % (i, j),
                (x, y, -.274), (.26, .010, .002),
                "InkDark", .001, False)
            box("Locker_VentHood_%s_%s" % (i, j),
                (x, y + .006, -.280), (.28, .009, .017),
                mat, .003, False)
        pipe("Locker_Pull_%d" % i,
             [(x + .15, 1.05, -.275), (x + .15, 1.05, -.313),
              (x + .15, .90, -.313), (x + .15, .90, -.275)],
             .009, "Frame")
        for j, y in enumerate((.42, 1.51)):
            box("Locker_HingeLeaf_%s_%s" % (i, j),
                (x - .214, y, -.277), (.037, .078, .009),
                "Frame", .003)
            lathe("Locker_HingeBarrel_%s_%s" % (i, j),
                  (x - .231, y - .041, -.282),
                  [(0, .009), (.082, .009)], "Frame",
                  sides=api.N(12, 8, 6))
        if i == 1:
            box("Locker_RepairPatch", (x - .065, .48, -.278),
                (.19, .23, .009), "Frame", .003)
            if api.N(1, 1, 0):
                for dx in (-.075, .075):
                    for dy in (-.095, .095):
                        bolt("Locker_PatchRivet_%s_%s" % (dx, dy),
                             x - .065 + dx, .48 + dy, -.287, .004)
    pipe("Locker_PadlockShackle",
         [(.16, .885, -.315), (.16, .945, -.315),
          (.18, .966, -.315), (.20, .945, -.315),
          (.20, .885, -.315)], .005, "Frame", api.N(10, 8, 6))
    box("Locker_PadlockBody", (.18, .873, -.314), (.063, .065, .026),
        "Copper", .011)
    box("Locker_PadlockKeySlot", (.18, .872, -.329), (.004, .017, .002),
        "InkDark", .0005, False)
    pipe("Locker_WireHook",
         [(.69, 1.49, -.278), (.69, 1.49, -.34),
          (.69, 1.405, -.35), (.69, 1.39, -.326)],
         .004, "Frame", api.N(10, 8, 6))
    box("Locker_PersonalPicture", (-.54, 1.045, -.272),
        (.125, .17, .002), "Paper", .001, False, -4)
    box("Locker_PictureSky", (-.54, 1.075, -.2738),
        (.105, .065, .0015), "CanvasBlue", .001, False, -4)
    box("Locker_PictureLand", (-.54, 1.018, -.2740),
        (.105, .045, .0015), "CanvasBeige", .001, False, -4)
    label("Locker_PictureCaption", "ДОМ", (-.581, .984, -.275), .019,
          "InkDark", -4)
    for x in (-.591, -.487):
        pipe("Locker_PhotoMagnet_" + str(x),
                 [(x, 1.116, -.274), (x, 1.116, -.280)],
             .007, "SignRed", api.N(12, 8, 6))
    api.end()

    # Community board: timber backing and separate paper objects with spatial
    # information, pins, strings, annotations and a ruled shared ledger.
    api.begin("KH_CommunityBoard",
              "Reclaimed community notice board with timber frame, pinned "
              "neighbourhood map, job notices and a hand-ruled duty ledger")
    for x in (-.93, .93):
        box("Board_Post_" + str(x), (x, .85, .035), (.072, 1.70, .075),
            "TimberRaw", .006)
        box("Board_Foot_" + str(x), (x, .018, 0), (.16, .036, .18),
            "Frame", .005)
    for i in range(9):
        x = -.99 + i * .2475
        box("Board_BackingPlank_%02d" % i, (x, 1.11, .010),
            (.244, 1.26, .035), "WornWood", .003)
    for x in (-1.065, 1.065):
        box("Board_FrameSide_" + str(x), (x, 1.11, -.013),
            (.07, 1.38, .07), "TimberRaw", .007)
    for y in (.45, 1.77):
        box("Board_FrameCross_" + str(y), (0, y, -.013),
            (2.20, .066, .07), "TimberRaw", .007)
    for x in (-1.065, 1.065):
        for y in (.45, 1.77):
            bolt("Board_FrameBolt_%s_%s" % (x, y), x, y, -.054, .010)
    box("Board_HeaderPaper", (-.02, 1.625, -.009),
        (1.67, .17, .002), "CanvasBeige", .002, False)
    label("Board_Header", "ОБЩИНА / КУРС", (-.47, 1.545, -.012), .12,
          "InkDark")
    label("Board_HeaderSmall", "ОБЪЯВЛЕНИЯ", (.45, 1.60, -.012), .035,
          "InkDark")
    papers = (
        ("Map", -.48, 1.11, .84, .63, -2),
        ("RepairJobs", .40, 1.345, .31, .34, 3),
        ("MedicNotice", .80, 1.29, .30, .43, -4),
        ("DutyLedger", .46, .79, .69, .38, 1),
        ("SmallNote", -.90, .66, .19, .18, -6),
    )
    for name, x, y, w, h, angle in papers:
        box("Board_Paper_" + name, (x, y, -.009), (w, h, .0025),
            "Paper", .0007, False, angle)
        # Pin heads are perpendicular to the board, not upright lathe pegs.
        for dx in (-w * .40, w * .40):
            pipe("Board_Pin_%s_%s" % (name, dx),
                 [(x + dx, y + h * .41, -.010),
                  (x + dx, y + h * .41, -.020)],
                 .006, "Copper", api.N(10, 8, 6))
    # Four streets and a river explain the notice board's real map purpose.
    for index, y in enumerate((.91, 1.07, 1.24)):
        board_seam("Board_MapStreetH_%d" % index, -.86, -.12,
                   y, -.0118, "CanvasBeige")
    for index, x in enumerate((-.73, -.49, -.26)):
        box("Board_MapStreetV_%d" % index,
            (x, 1.10, -.0118), (.004, .52, .00025),
            "CanvasBeige", .00005, False)
    pipe("Board_MapRiver",
         [(-.85, 1.36, -.012), (-.70, 1.29, -.012),
          (-.66, 1.12, -.012), (-.55, 1.01, -.012),
          (-.60, .84, -.012)], .006, "CanvasBlue", api.N(8, 6, 4))
    for index, (x, y) in enumerate(((-.80, 1.14), (-.41, 1.13),
                                  (-.38, .98), (-.20, 1.29))):
        box("Board_MapBlock_%d" % index, (x, y, -.0118),
            (.067, .060, .0015), "CanvasDark", .001, False)
    box("Board_MapHub", (-.47, 1.22, -.013), (.085, .067, .002),
        "SignRed", .002, False)
    label("Board_MapTitle", "КВАРТАЛ / ПОСТЫ", (-.85, 1.355, -.0135),
          .031, "InkDark")
    label("Board_MapHubLabel", "МЫ ЗДЕСЬ", (-.43, 1.20, -.015),
          .019, "InkDark")
    label("Board_MapNorth", "СЕВЕР", (-.31, 1.31, -.0135),
          .021, "InkDark")
    label("Board_RepairHeading", "НУЖНЫ РУКИ", (.272, 1.46, -.012),
          .030, "InkDark")
    label("Board_RepairText", "КРЫША\nФИЛЬТР\nШВЕЯ", (.29, 1.29, -.012),
          .025, "InkDark")
    label("Board_MedicHeading", "МЕДПУНКТ", (.675, 1.45, -.012),
          .028, "InkDark")
    box("Board_MedicCrossV", (.79, 1.33, -.0118), (.017, .076, .0015),
        "SignRed", .001, False)
    box("Board_MedicCrossH", (.79, 1.33, -.012), (.071, .017, .0015),
        "SignRed", .001, False)
    label("Board_MedicHours", "ПЕРЕВЯЗКИ\nУТРО / ВЕЧЕР", (.674, 1.19, -.012),
          .022, "InkDark")
    label("Board_LedgerHeading", "ДЕЖУРСТВА / ВОДА", (.16, .91, -.012),
          .032, "InkDark")
    for i in range(api.N(7, 5, 3)):
        board_seam("Board_LedgerRow_%d" % i, .16, .76,
                   .64 + i * .035, -.0118)
    for x in (.37, .57):
        pipe("Board_LedgerColumn_" + str(x),
             [(x, .635, -.0118), (x, .88, -.0118)], .0008,
             "InkDark", api.N(6, 6, 4))
    if api.N(1, 1, 0):
        label("Board_LedgerNames", "АННА    06:00\nИЛЬЯ     12:00\nМИША   18:00",
              (.18, .78, -.012), .021, "InkDark")
        label("Board_Note", "НЕ ЗАБЫТЬ\nСЕМЕНА", (-.974, .62, -.012),
              .021, "InkDark")
        pipe("Board_MapMarkingString",
             [(-.47, 1.22, -.025), (-.38, 1.03, -.025),
              (-.20, 1.29, -.025)], .0017, "SignRed", api.N(6, 6, 4))
    api.end()
