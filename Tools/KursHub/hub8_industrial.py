"""Author-owned service equipment for KursHub revision 8.

All dimensions use metres, Y is up, and the working face points towards -Z.
The module uses the revision-8 Builder rather than vendor geometry. Each casting,
panel, hose, fastener and fitting is a separate closed manufactured part.
"""

from math import cos, sin, pi


def _cylinder(api, name, a, b, radius, mat="Frame", sides=None, collision=True):
    api.pipe(name, points=[a, b], radius=radius, mat=mat,
             sides=sides or api.N(24, 16, 10), collision=collision)


def _bolt(api, name, x, y, z, axis="z", radius=.013):
    if axis == "y":
        a, b = (x, y-.005, z), (x, y+.005, z)
    elif axis == "x":
        a, b = (x-.005, y, z), (x+.005, y, z)
    else:
        a, b = (x, y, z-.005), (x, y, z+.005)
    _cylinder(api, name, a, b, radius, "Frame", api.N(6, 6, 6), False)


def _gauge(api, name, x, y, z, radius=.072, text="бар"):
    # A glazed instrument with a real bezel, dial, and raised needle.
    _cylinder(api, name+"_case", (x,y,z+.035), (x,y,z), radius, "Frame", collision=False)
    _cylinder(api, name+"_dial", (x,y,z-.001), (x,y,z-.006), radius*.87, "Paper", collision=False)
    _cylinder(api, name+"_glass", (x,y,z-.008), (x,y,z-.010), radius*.84, "Glass", collision=False)
    for j in range(api.N(11, 7, 5)):
        t = pi*.14 + j*(pi*1.72)/(api.N(11,7,5)-1)
        r0,r1 = radius*.62,radius*.75
        _cylinder(api, name+"_tick_%02d"%j,
                  (x+cos(t)*r0,y+sin(t)*r0,z-.012),
                  (x+cos(t)*r1,y+sin(t)*r1,z-.012), .0017,
                  "PlasticDark", api.N(6, 6, 4), False)
    _cylinder(api, name+"_needle", (x,y,z-.014),
              (x-radius*.39,y+radius*.40,z-.014), .0024,
              "SignRed", api.N(8, 6, 4), False)
    _cylinder(api, name+"_pivot", (x,y,z-.013), (x,y,z-.018), radius*.075,
              "Frame", api.N(12,8,6), False)
    if api.N(1,1,0):
        api.text(name+"_units", text, (x-radius*.30,y-radius*.34,z-.016),
                 size=radius*.25, mat="Lettering")


def _front_plate(api, name, text, center, width, height, mat="Enamel", size=.035):
    x,y,z=center
    api.box(name+"_plate", center, (width,height,.012), mat=mat,
            bevel=.003, collision=False)
    api.text(name+"_print", text, (x-width*.44,y-height*.12,z-.008),
             size=size, mat="Lettering")
    for j,dx in enumerate((-width*.43,width*.43)):
        _bolt(api,name+"_screw_"+str(j),x+dx,y,z-.009,radius=.004)


def _canister(api, name, x, y, z, mat="Enamel", scale=1.):
    api.box(name+"_pressed_body", (x,y+.22*scale,z),
            (.28*scale,.40*scale,.15*scale), mat=mat, bevel=.035*scale)
    api.box(name+"_shoulder", (x,y+.425*scale,z),
            (.24*scale,.07*scale,.14*scale), mat=mat, bevel=.025*scale)
    for k,dx in enumerate((-.085,.085)):
        api.pipe(name+"_handle_"+str(k),
                 [(x+dx*scale,y+.43*scale,z),
                  (x+dx*scale,y+.50*scale,z),
                  (x-dx*.1*scale,y+.50*scale,z)],
                 radius=.012*scale,mat="Frame",sides=api.N(12,8,6),collision=False)
    api.lathe(name+"_filler", (x+.077*scale,y+.444*scale,z-.035*scale),
              profile=[(0,.026*scale),(.019*scale,.026*scale),(.025*scale,.021*scale)],
              mat="Frame",sides=api.N(20,12,8),collision=False)
    # Pressed ribs on both faces are structural rather than a painted cross.
    for face in (-1,1):
        for j in (-1,1):
            api.pipe(name+"_rib_%s_%s"%(face,j),
                     [(x-j*.09*scale,y+.10*scale,z+face*.077*scale),
                      (x+j*.09*scale,y+.33*scale,z+face*.077*scale)],
                     radius=.006*scale,mat=mat,sides=api.N(8,6,4),collision=False)


def _diesel_generator(api):
    api.begin("KH_DieselGenerator", "Дизельная электростанция: сварная рама, двигатель, альтернатор, радиатор и топливная система")
    # Fabricated skid: paired channel webs/flanges and transverse members.
    for z in (-.53,.53):
        api.box("Skid_web_%s"%z,(0,.10,z),(2.8,.17,.025),"Frame",bevel=.004)
        for y in (.009,.185):
            api.box("Skid_flange_%s_%s"%(z,y),(0,y,z),(2.8,.018,.13),"Frame",bevel=.003)
    for x in (-1.19,-.45,.44,1.16):
        api.box("Skid_cross_%s"%x,(x,.12,0),(.09,.11,1.16),"Frame",bevel=.005)
        for z in (-.51,.51):
            api.box("Isolation_rubber_%s_%s"%(x,z),(x,.23,z),(.14,.08,.13),"PlasticDark",bevel=.014)
            _bolt(api,"Skid_mount_%s_%s"%(x,z),x,.278,z,"y",.017)
    # Alternator is built from cylindrical castings along the engine axis.
    for i,(a,b,r,mat) in enumerate(((-1.22,-1.16,.275,"Frame"),(-1.16,-.61,.305,"Enamel"),(-.61,-.54,.26,"Frame"))):
        _cylinder(api,"Alternator_casting_%s"%i,(a,.64,0),(b,.64,0),r,mat)
    for j in range(api.N(18,12,8)):
        t=2*pi*j/api.N(18,12,8)
        y,z=.64+cos(t)*.307,sin(t)*.307
        _cylinder(api,"Alternator_cooling_fin_%02d"%j,(-1.13,y,z),(-.67,y,z),.010,"Frame",api.N(8,6,4),False)
    _cylinder(api,"Alternator_coupling",(-.54,.64,0),(-.39,.64,0),.17,"Frame")
    api.box("Alternator_terminal_box",(-.88,1.015,.015),(.39,.18,.29),"Enamel",bevel=.025)
    api.box("Terminal_lid",(-.88,1.111,.015),(.40,.018,.30),"Frame",bevel=.006)
    for x in (-1.03,-.73):
        for z in (-.095,.125):
            _bolt(api,"Terminal_lid_bolt_%s_%s"%(x,z),x,1.124,z,"y",.008)
    # Lower crankcase, oil sump, and six individually cast cylinder heads.
    api.box("Engine_crankcase",(.22,.59,0),(.99,.46,.53),"SteelOxide",bevel=.065)
    api.box("Engine_oil_sump",(.20,.34,0),(.81,.15,.42),"Frame",bevel=.045)
    api.box("Engine_block",(.22,.855,0),(1.04,.20,.57),"Enamel",bevel=.032)
    for j in range(6):
        x=-.20+j*.165
        api.box("Cylinder_head_%02d"%j,(x,.997,0),(.146,.11,.52),"SteelOxide",bevel=.020)
        _cylinder(api,"Injector_%02d"%j,(x,1.045,-.12),(x,1.11,-.12),.019,"Frame",api.N(12,8,6),False)
        api.pipe("Injector_line_%02d"%j,[(x,1.102,-.12),(x,1.145,-.18),(x,1.08,-.33),(x,.80,-.35)],
                 .005,"Copper",sides=api.N(10,8,6),collision=False)
        _bolt(api,"Rocker_cover_bolt_%02d"%j,x,1.058,.15,"y",.009)
    api.pipe("Fuel_rail",[(-.22,.79,-.35),(.67,.79,-.35)],.025,"Frame",sides=api.N(16,10,8),collision=False)
    api.lathe("Fuel_filter",(-.18,.47,-.36),[(0,.064),(.18,.064),(.195,.05)],"Enamel",sides=api.N(24,16,10))
    api.box("Fuel_tank",(-.65,1.33,.12),(.86,.30,.61),"Enamel",bevel=.075)
    api.lathe("Fuel_cap",(-.87,1.489,.10),[(0,.056),(.025,.056),(.038,.047)],"Frame",sides=api.N(24,16,10),collision=False)
    api.pipe("Fuel_supply_hose",[(-.43,1.20,-.16),(-.47,1.05,-.40),(-.35,.75,-.43),(-.18,.67,-.40)],
             .012,"PlasticDark",sides=api.N(14,10,8),collision=False)
    # Fan/radiator assembly has a real air gap, protective grid, and hoses.
    _cylinder(api,"Fan_shroud",(.83,.78,0),(.92,.78,0),.40,"PlasticDark")
    api.box("Radiator_core",(1.075,.81,0),(.16,.99,.91),"PlasticDark",bevel=.020)
    for z in (-.49,.49):
        api.box("Radiator_side_rail_%s"%z,(1.08,.81,z),(.26,1.08,.055),"Frame",bevel=.010)
    for y in (.285,1.335):
        api.box("Radiator_header_%s"%y,(1.08,y,0),(.27,.085,1.02),"Enamel",bevel=.023)
    for j in range(api.N(20,12,8)):
        y=.35+j*.91/(api.N(20,12,8)-1)
        api.box("Radiator_fin_%02d"%j,(1.164,y,0),(.012,.016,.90),"Frame",bevel=.002,collision=False)
    for z in (-.32,0,.32):
        api.box("Radiator_guard_%s"%z,(1.20,.82,z),(.018,.96,.014),"Frame",bevel=.002,collision=False)
    api.pipe("Coolant_upper_hose",[(.64,1.02,.27),(.75,1.22,.34),(1.02,1.28,.34)],.044,"PlasticDark",sides=api.N(20,14,10))
    api.pipe("Coolant_lower_hose",[(.67,.46,.24),(.82,.33,.33),(1.03,.35,.33)],.039,"PlasticDark",sides=api.N(18,12,8))
    # Exhaust retains separation from the fuel tank and front working face.
    api.pipe("Exhaust_manifold",[(-.19,.91,.32),(.64,.91,.32),(.67,1.27,.40)],.042,"SteelOxide",sides=api.N(20,14,10))
    _cylinder(api,"Exhaust_silencer",(.08,1.39,.42),(.68,1.39,.42),.105,"SteelOxide")
    api.pipe("Exhaust_outlet",[(.08,1.39,.42),(-.12,1.39,.42),(-.19,1.57,.42)],.037,"SteelOxide",sides=api.N(18,12,8))
    for x in (.18,.58):
        api.box("Silencer_saddle_%s"%x,(x,1.28,.42),(.06,.05,.21),"Frame",bevel=.006)
    # Small control console at the accessible face, not a self-lit floating panel.
    api.box("Control_box",(-.87,.67,-.48),(.54,.37,.20),"Enamel",bevel=.020)
    _gauge(api,"Frequency_meter",-.98,.72,-.587,.068,"Гц")
    _gauge(api,"Voltage_meter",-.77,.72,-.587,.068,"В")
    _cylinder(api,"Emergency_stop",(-.68,.55,-.586),(-.68,.55,-.64),.030,"SignRed",collision=False)
    api.pipe("Output_cable",[(-.75,.52,-.48),(-.62,.36,-.59),(-.35,.22,-.61),(.08,.20,-.60)],.022,"PlasticDark",sides=api.N(16,10,8),collision=False)
    _front_plate(api,"Generator_nameplate","ДЭС-30  /  380 В",(-.91,1.33,-.195),.62,.12,size=.032)
    _front_plate(api,"Generator_service","ТОПЛИВО: ДИЗЕЛЬ",(.27,.89,-.293),.65,.075,size=.028)
    api.end()


def _power_switchboard(api):
    api.begin("KH_PowerSwitchboard", "Распределительный щит 380/220 В с дверями, приборами, автоматами, замками и кабельными вводами")
    api.box("Plinth",(0,.08,0),(1.6,.16,.45),"Frame",bevel=.012)
    api.box("Backplate",(0,1.08,.205),(1.57,1.84,.025),"Frame",bevel=.004)
    for x in (-.782,.782):
        api.box("Enclosure_side_%s"%x,(x,1.08,0),(.025,1.86,.44),"Enamel",bevel=.006)
    for y in (.16,2.005):
        api.box("Enclosure_end_%s"%y,(0,y,0),(1.6,.035,.45),"Enamel",bevel=.006)
    for j,x in enumerate((-.393,.393)):
        api.box("Door_gasket_%s"%j,(x,1.08,-.215),(.762,1.80,.016),"PlasticDark",bevel=.008)
        api.box("Pressed_door_%s"%j,(x,1.08,-.232),(.744,1.784,.026),"Enamel",bevel=.012)
        for y in (.38,1.08,1.78):
            hx=x+(-.37 if j==0 else .37)
            _cylinder(api,"Door_hinge_%s_%s"%(j,y),(hx,y-.05,-.248),(hx,y+.05,-.248),.014,"Frame",api.N(16,10,8),False)
        hx=x+(.27 if j==0 else -.27)
        _cylinder(api,"Lock_%s"%j,(hx,1.02,-.247),(hx,1.02,-.267),.020,"Frame",collision=False)
        api.pipe("Door_handle_%s"%j,[(hx,.89,-.25),(hx,.89,-.286),(hx,1.01,-.286),(hx,1.01,-.25)],
                 .010,"Frame",sides=api.N(14,10,8),collision=False)
    _gauge(api,"Input_voltmeter",-.40,1.69,-.258,.094,"В")
    _gauge(api,"Input_ammeter",.39,1.69,-.258,.094,"А")
    for j in range(6):
        x=-.64+j*.253
        api.box("Breaker_recess_%s"%j,(x,1.31,-.254),(.18,.23,.016),"PlasticDark",bevel=.006,collision=False)
        api.box("Breaker_body_%s"%j,(x,1.31,-.275),(.128,.186,.028),"Paper",bevel=.007,collision=False)
        api.box("Breaker_toggle_%s"%j,(x,1.319,-.299),(.054,.052,.022),"PlasticDark",bevel=.004,collision=False)
        api.box("Breaker_status_%s"%j,(x,1.369,-.293),(.032,.014,.006),"SignRed" if j==5 else "Enamel",bevel=.001,collision=False)
    for j in range(api.N(12,8,5)):
        y=.37+j*.022
        for x in (-.39,.39):
            api.box("Door_vent_%s_%s"%(x,j),(x,y,-.253),(.42,.008,.012),"PlasticDark",bevel=.002,collision=False)
    for j,x in enumerate((-.58,-.20,.20,.58)):
        api.lathe("Top_gland_%s"%j,(x,2.024,0),[(0,.039),(.025,.039),(.035,.028)],"Frame",sides=api.N(20,12,8),collision=False)
        api.pipe("Top_cable_%s"%j,[(x,2.047,0),(x,2.09,0),(x+.04,2.09,.15)],.019,"PlasticDark",sides=api.N(16,10,8),collision=False)
    _front_plate(api,"Main_identification","ГРЩ-1  380/220 В",(0,1.913,-.258),.71,.105,size=.038)
    _front_plate(api,"Left_circuits","СВЕТ  /  МЕДПУНКТ",(-.39,.78,-.257),.60,.075,size=.026)
    _front_plate(api,"Right_circuits","НАСОС  /  МАСТЕРСКАЯ",(.39,.78,-.257),.64,.075,size=.023)
    _front_plate(api,"Danger","ОПАСНО: НАПРЯЖЕНИЕ",(0,.22,-.258),.74,.075,"SignRed",.027)
    api.end()


def _water_treatment(api):
    api.begin("KH_WaterTreatment", "Установка водоподготовки: накопитель, последовательные фильтры, насос, краны, манометр и отводы")
    for x in (-1.49,1.49):
        for z in (-.48,.48):
            api.box("Frame_upright_%s_%s"%(x,z),(x,1.00,z),(.055,1.94,.055),"Frame",bevel=.005)
            api.box("Footplate_%s_%s"%(x,z),(x,.0175,z),(.18,.035,.18),"Frame",bevel=.006)
            _bolt(api,"Anchor_%s_%s"%(x,z),x+.05,.040,z,"y",.014)
    for y in (.12,1.97):
        for z in (-.48,.48):
            api.box("Frame_cross_%s_%s"%(y,z),(0,y,z),(3.03,.055,.055),"Frame",bevel=.005)
        for x in (-1.49,1.49):
            api.box("Frame_end_%s_%s"%(y,x),(x,y,0),(.055,.055,1.01),"Frame",bevel=.005)
    api.box("Tank_cradle",(-.80,.18,.02),(1.15,.09,1.03),"Frame",bevel=.012)
    api.lathe("Water_accumulator",(-.80,.225,.02),
              [(0,.36),(.04,.45),(.12,.485),(1.33,.485),(1.43,.44),(1.48,.32)],
              "Enamel",sides=api.N(48,32,20))
    for y in (.43,1.49):
        api.lathe("Tank_reinforcing_band_%s"%y,(-.80,y,.02),[(0,.49),(.027,.49)],"Frame",sides=api.N(48,32,20),collision=False)
    api.lathe("Tank_neck",(-.80,1.704,.02),[(0,.12),(.055,.12),(.069,.145),(.088,.145)],"Frame",sides=api.N(32,20,12))
    api.box("Tank_cover_handle",(-.80,1.822,.02),(.16,.025,.035),"Frame",bevel=.008,collision=False)
    for j,x in enumerate((.08,.58)):
        api.lathe("Filter_housing_%s"%j,(x,.59,.10),
                  [(0,.08),(.035,.13),(.10,.145),(.80,.145),(.87,.12),(.91,.12)],
                  "Paper" if j==0 else "Enamel",sides=api.N(40,24,16))
        api.lathe("Filter_head_%s"%j,(x,1.49,.10),[(0,.17),(.09,.17),(.11,.14)],"Frame",sides=api.N(32,20,12))
        for y in (.75,1.30):
            api.lathe("Filter_clamp_%s_%s"%(j,y),(x,y,.10),[(0,.152),(.020,.152)],"Frame",sides=api.N(32,20,12),collision=False)
            api.box("Filter_wall_bracket_%s_%s"%(j,y),(x,y,.34),(.30,.045,.25),"Frame",bevel=.005)
    # Floor-mounted motor and volute casting have distinct silhouettes.
    api.box("Pump_base",(1.10,.21,.03),(.56,.075,.45),"Frame",bevel=.009)
    _cylinder(api,"Pump_motor",(1.10,.40,-.12),(1.10,.40,.27),.14,"Enamel")
    for j in range(api.N(12,8,6)):
        t=j*2*pi/api.N(12,8,6)
        _cylinder(api,"Pump_motor_rib_%s"%j,(1.10+cos(t)*.145,.40+sin(t)*.145,-.09),
                  (1.10+cos(t)*.145,.40+sin(t)*.145,.23),.006,"Frame",api.N(8,6,4),False)
    _cylinder(api,"Pump_volute",(1.10,.40,-.25),(1.10,.40,-.12),.18,"Frame")
    api.pipe("Raw_feed",[(-.35,.41,.02),(-.18,.41,.02),(-.18,.40,-.31),(.92,.40,-.31),(1.10,.40,-.25)],
             .027,"Copper",sides=api.N(20,14,10))
    api.pipe("Pump_to_first_filter",[(1.10,.57,-.19),(1.10,1.68,-.19),(.08,1.68,-.19),(.08,1.57,.04)],
             .023,"Copper",sides=api.N(20,14,10))
    api.pipe("Filter_interconnect",[(.08,1.56,.22),(.08,1.72,.30),(.58,1.72,.30),(.58,1.56,.22)],
             .023,"Copper",sides=api.N(20,14,10))
    api.pipe("Filtered_outlet",[(.58,1.55,-.03),(.58,1.60,-.34),(1.40,1.60,-.34),(1.40,.96,-.34),(1.57,.96,-.34)],
             .023,"Copper",sides=api.N(20,14,10))
    for j,(x,y,z) in enumerate(((-.15,.40,-.31),(1.40,1.28,-.34))):
        api.box("Valve_body_%s"%j,(x,y,z),(.085,.085,.075),"Copper",bevel=.015)
        _cylinder(api,"Valve_stem_%s"%j,(x,y,z-.035),(x,y,z-.105),.013,"Frame",collision=False)
        _cylinder(api,"Valve_T_handle_%s"%j,(x-.095,y,z-.11),(x+.095,y,z-.11),.014,"SignRed",collision=False)
    api.pipe("Gauge_siphon",[(.91,1.60,-.34),(.91,1.79,-.34),(.91,1.79,-.39)],.009,"Copper",sides=api.N(14,10,8),collision=False)
    _gauge(api,"Water_pressure",.91,1.81,-.40,.077,"бар")
    api.pipe("Electrical_feed",[(1.15,.42,.28),(1.34,.49,.36),(1.39,.86,.42)],.012,"PlasticDark",sides=api.N(14,10,8),collision=False)
    api.box("Pump_isolator",(1.39,.90,.40),(.15,.23,.09),"Enamel",bevel=.015)
    # The broad flat inventory plate is mounted on short studs at the curved tank.
    for x in (-1.07,-.53):
        _cylinder(api,"Tank_plate_stud_%s"%x,(x,1.15,-.375),(x,1.15,-.473),.009,
                  "Frame",api.N(12,8,6),False)
    _front_plate(api,"Raw_tank_label","НАКОПИТЕЛЬ  250 Л",(-.80,1.15,-.475),.73,.11,size=.032)
    _front_plate(api,"Filter_1_label","01  МЕХ.",(.08,1.13,-.055),.21,.09,size=.027)
    _front_plate(api,"Filter_2_label","02  УГОЛЬ",(.58,1.13,-.055),.23,.09,size=.026)
    _front_plate(api,"Treatment_header","ВОДОПОДГОТОВКА",(.50,1.96,-.518),1.23,.105,size=.047)
    api.end()


def _utility_rack(api):
    api.begin("KH_UtilityRack", "Сборный складской стеллаж с перфорированными стойками, четырьмя полками, тарой и запасными шлангами")
    # Open ladder slots are made from rail pairs and ties, not painted black dots.
    for x in (-1.18,1.18):
        for z in (-.34,.34):
            for dx in (-.018,.018):
                api.box("Post_slot_rail_%s_%s_%s"%(x,z,dx),(x+dx,1.10,z),(.012,2.2,.022),"Frame",bevel=.002)
            api.box("Post_return_%s_%s"%(x,z),(x,1.10,z+.026),(.049,2.2,.020),"Frame",bevel=.003)
            count=api.N(24,16,10)
            for j in range(count):
                api.box("Post_slot_tie_%s_%s_%02d"%(x,z,j),(x,.03+j*2.14/(count-1),z),(.048,.016,.022),"Frame",bevel=.002,collision=False)
            api.box("Rack_foot_%s_%s"%(x,z),(x,.017,z),(.14,.034,.13),"Frame",bevel=.005)
    for level,y in enumerate((.16,.77,1.38,1.99)):
        for z in (-.34,.34):
            api.box("Shelf_front_beam_%s_%s"%(level,z),(0,y-.025,z),(2.40,.075,.038),"Enamel",bevel=.004)
        for j,z in enumerate((-.255,-.085,.085,.255)):
            api.box("Shelf_board_%s_%s"%(level,j),(0,y+.023,z),(2.34,.036,.16),"TimberRaw",bevel=.004)
        for x in (-1.15,1.15):
            api.box("Shelf_end_%s_%s"%(level,x),(x,y-.025,0),(.045,.075,.68),"Frame",bevel=.003)
            _bolt(api,"Shelf_bolt_%s_%s"%(level,x),x,y-.025,-.367,radius=.011)
    for a,b in (((-1.16,.22,.36),(1.16,1.94,.36)),((1.16,.22,.36),(-1.16,1.94,.36))):
        api.pipe("Rear_cross_brace_%s"%str(a[0]),[a,b],.010,"Frame",sides=api.N(10,8,6),collision=False)
    # Two open steel bins on the middle shelf. Their interiors remain visible.
    for j,x in enumerate((-.75,-.12)):
        api.box("Bin_base_%s"%j,(x,.815,-.015),(.51,.035,.50),"Enamel",bevel=.008)
        for dx in (-.248,.248):
            api.box("Bin_side_%s_%s"%(j,dx),(x+dx,.985,-.015),(.024,.32,.50),"Enamel",bevel=.006)
        for z in (-.25,.22):
            api.box("Bin_end_%s_%s"%(j,z),(x,.975,z),(.49,.30,.022),"Enamel",bevel=.006)
        api.pipe("Bin_handle_%s"%j,[(x-.09,.965,-.269),(x-.09,.965,-.302),(x+.09,.965,-.302),(x+.09,.965,-.269)],
                 .007,"Frame",sides=api.N(10,8,6),collision=False)
        for k in range(api.N(7,5,3)):
            api.box("Bin_spare_%s_%s"%(j,k),(x-.17+k*.05,.90+(k%2)*.045,-.04),(.035,.12,.17),"Frame",bevel=.005,yaw=(-18+k*13),collision=False)
        _front_plate(api,"Bin_inventory_%s"%j,"КРЕПЁЖ" if j==0 else "ЗАПЧАСТИ",(x,1.03,-.269),.36,.075,size=.027)
    _canister(api,"Rack_canister_A",.60,.805,-.02,"Enamel",.82)
    _canister(api,"Rack_canister_B",.96,.805,.01,"SteelOxide",.72)
    # Small service bottles on the upper shelf include shoulders and screw caps.
    for j,x in enumerate((-.91,-.64,-.37)):
        api.lathe("Service_bottle_%s"%j,(x,1.425,-.01),[(0,.082),(.23,.082),(.27,.04),(.32,.04)],"PlasticDark",sides=api.N(24,16,10))
        api.lathe("Service_cap_%s"%j,(x,1.745,-.01),[(0,.044),(.028,.044)],"SignRed",sides=api.N(16,12,8),collision=False)
        _front_plate(api,"Service_bottle_label_%s"%j,"МАСЛО",(x,1.55,-.097),.09,.05,size=.015)
    api.box("Top_fabric_bundle",(.62,2.075,.04),(.78,.13,.47),"CanvasDark",bevel=.050)
    for x in (.35,.89):
        api.box("Bundle_strap_%s"%x,(x,2.075,.04),(.026,.135,.475),"PlasticDark",bevel=.005,collision=False)
    # Coiled hose lies on the lower shelf; no closed-curve overlapping end caps.
    pts=[]
    count=api.N(96,64,40)
    for j in range(count):
        t=j/(count-1)*pi*5.2
        r=.17+.065*j/(count-1)
        pts.append((-.69+cos(t)*r,.218+.016*j/(count-1),sin(t)*r))
    api.pipe("Coiled_service_hose",pts,.018,"PlasticDark",sides=api.N(12,8,6),collision=False)
    api.pipe("Hose_free_tail",[pts[-1],(-.37,.23,-.20),(-.15,.23,-.20)],.018,"PlasticDark",sides=api.N(12,8,6),collision=False)
    api.box("Bottom_parts_case",(.62,.31,.02),(.78,.23,.49),"Frame",bevel=.025)
    for x in (.37,.86):
        api.box("Case_latch_%s"%x,(x,.33,-.235),(.035,.077,.025),"Frame",bevel=.004,collision=False)
    _front_plate(api,"Rack_header","ЗАПАСНЫЕ МАТЕРИАЛЫ",(0,2.012,-.368),1.03,.085,size=.033)
    api.end()


def _repair_bench(api):
    api.begin("KH_RepairBench", "Слесарный верстак: досчатая столешница, шкаф, ящики, тиски, точило, инструмент и рабочий свет")
    for x in (-1.12,1.12):
        for z in (-.32,.32):
            api.box("Leg_web_%s_%s"%(x,z),(x,.43,z),(.048,.82,.048),"Frame",bevel=.005)
            api.box("Leg_foot_%s_%s"%(x,z),(x,.0165,z),(.17,.033,.15),"Frame",bevel=.006)
    for z in (-.32,.32):
        api.box("Bench_stringer_%s"%z,(0,.79,z),(2.30,.09,.06),"Frame",bevel=.006)
        api.box("Lower_stringer_%s"%z,(0,.19,z),(2.30,.045,.045),"Frame",bevel=.004)
    for j,z in enumerate((-.30,0,.30)):
        api.box("Solid_wood_top_%s"%j,(0,.872,z),(2.5,.095,.292),"WornWood",bevel=.012)
    for x in (-1.10,1.10):
        for z in (-.32,.32):
            _bolt(api,"Top_carriage_bolt_%s_%s"%(x,z),x,.923,z,"y",.011)
    # Drawer housing uses sheet panels around separate drawer fronts.
    api.box("Cabinet_back",(-.77,.47,.285),(.61,.52,.025),"Enamel",bevel=.006)
    for x in (-1.08,-.46):
        api.box("Cabinet_side_%s"%x,(x,.47,.01),(.024,.52,.57),"Enamel",bevel=.006)
    api.box("Cabinet_bottom",(-.77,.21,.01),(.64,.028,.57),"Enamel",bevel=.005)
    for j,y in enumerate((.32,.49,.66)):
        api.box("Drawer_front_%s"%j,(-.77,y,-.289),(.585,.148,.035),"Enamel",bevel=.008)
        api.pipe("Drawer_pull_%s"%j,[(-.89,y,-.31),(-.89,y,-.345),(-.65,y,-.345),(-.65,y,-.31)],.008,"Frame",sides=api.N(12,8,6),collision=False)
    api.box("Lower_wood_shelf",(.36,.235,0),(1.45,.035,.60),"TimberRaw",bevel=.005)
    # Cast vise base and fixed/moving jaw, exposed screw, tommy bar, and slide.
    api.lathe("Vise_swivel_base",(-.52,.923,-.22),[(0,.14),(.035,.15),(.065,.11)],"Frame",sides=api.N(32,20,12))
    api.box("Vise_anvil",(-.52,1.02,-.06),(.24,.12,.20),"SteelOxide",bevel=.024)
    api.box("Vise_fixed_body",(-.52,1.04,-.24),(.23,.16,.20),"Frame",bevel=.024)
    api.box("Vise_fixed_jaw",(-.52,1.155,-.20),(.28,.075,.065),"Frame",bevel=.007)
    api.box("Vise_moving_body",(-.52,1.05,-.385),(.24,.14,.115),"Frame",bevel=.021)
    api.box("Vise_moving_jaw",(-.52,1.155,-.375),(.28,.075,.055),"Frame",bevel=.007)
    api.box("Vise_slide",(-.52,1.012,-.30),(.105,.05,.30),"SteelOxide",bevel=.004)
    _cylinder(api,"Vise_leadscrew",(-.52,1.063,-.21),(-.52,1.063,-.47),.018,"Frame",collision=False)
    _cylinder(api,"Vise_screw_collar",(-.52,1.063,-.43),(-.52,1.063,-.47),.036,"Frame",collision=False)
    _cylinder(api,"Vise_tommy_bar",(-.64,1.063,-.465),(-.40,1.063,-.465),.009,"Frame",collision=False)
    for x in (-.642,-.398):
        _cylinder(api,"Vise_bar_end_%s"%x,(x-.009,1.063,-.465),(x+.009,1.063,-.465),.015,"Frame",collision=False)
    # Bench grinder with twin abrasive stones, guards, tool rests, eye shields.
    api.box("Grinder_foot",(.73,.95,.015),(.43,.06,.30),"Enamel",bevel=.013)
    api.box("Grinder_pedestal",(.73,1.025,.015),(.23,.11,.20),"Frame",bevel=.017)
    _cylinder(api,"Grinder_motor",(.54,1.17,.015),(.92,1.17,.015),.132,"Enamel")
    for x in (.45,1.01):
        _cylinder(api,"Grinding_guard_%s"%x,(x-.040,1.17,.015),(x+.040,1.17,.015),.181,"Frame")
        _cylinder(api,"Abrasive_stone_%s"%x,(x-.047,1.17,-.024),(x+.047,1.17,-.024),.151,"SteelOxide")
        _cylinder(api,"Wheel_hub_%s"%x,(x-.052,1.17,-.024),(x+.052,1.17,-.024),.035,"Frame",collision=False)
        api.box("Grinder_tool_rest_%s"%x,(x,1.102,-.18),(.17,.020,.11),"Frame",bevel=.004)
        api.pipe("Eyeshield_arm_%s"%x,[(x,1.29,.05),(x,1.47,.03),(x,1.47,-.10)],.008,"Frame",sides=api.N(12,8,6),collision=False)
        api.box("Grinder_eye_shield_%s"%x,(x,1.445,-.115),(.19,.14,.008),"Glass",bevel=.008,collision=False)
    api.box("Grinder_switch",(.73,1.07,-.13),(.09,.06,.04),"PlasticDark",bevel=.006,collision=False)
    api.box("Grinder_button",(.73,1.075,-.155),(.027,.028,.012),"SignRed",bevel=.004,collision=False)
    # Actual timber backboard, metal edge rails, individual pins and hung tools.
    api.box("Tool_backboard",(0,1.39,.405),(2.40,.76,.035),"TimberRaw",bevel=.008)
    for y in (1.01,1.77):
        api.box("Backboard_edge_%s"%y,(0,y,.397),(2.44,.025,.055),"Frame",bevel=.004)
    for j in range(api.N(12,8,5)):
        x=-1.05+j*2.10/(api.N(12,8,5)-1)
        _cylinder(api,"Tool_pin_%02d"%j,(x,1.63,.38),(x,1.63,.32),.006,"Frame",api.N(10,8,6),False)
    for j,x in enumerate((-.92,-.67,-.40)):
        length=.22+j*.035
        api.box("Wrench_shank_%s"%j,(x,1.42,.343),(.022,length,.016),"Frame",bevel=.005,collision=False)
        for side in (-1,1):
            yy=1.42+side*length*.54
            api.box("Wrench_jaw_base_%s_%s"%(j,side),(x,yy,.343),(.067,.024,.022),"Frame",bevel=.006,collision=False)
            for dx in (-.027,.027):
                api.box("Wrench_jaw_prong_%s_%s_%s"%(j,side,dx),(x+dx,yy+side*.021,.343),(.015,.048,.022),"Frame",bevel=.004,collision=False)
    _cylinder(api,"Hung_hammer_handle",(-.08,1.27,.343),(-.08,1.58,.343),.014,"TimberRaw",collision=False)
    api.box("Hung_hammer_head",(-.08,1.585,.343),(.16,.05,.057),"Frame",bevel=.011,collision=False)
    for j,x in enumerate((.21,.37)):
        _cylinder(api,"Screwdriver_shaft_%s"%j,(x,1.26,.342),(x,1.47,.342),.004,"Frame",collision=False)
        _cylinder(api,"Screwdriver_grip_%s"%j,(x,1.46,.342),(x,1.60,.342),.017,"SignRed" if j==0 else "PlasticDark",collision=False)
    api.pipe("Grinder_power_cord",[(.80,1.05,.15),(.94,.96,.27),(1.16,.92,.30),(1.16,.55,.31)],.007,"PlasticDark",sides=api.N(12,8,6),collision=False)
    api.lathe("Task_lamp_base",(-1.05,.923,.10),[(0,.095),(.028,.095),(.046,.065)],"Frame",sides=api.N(24,16,10))
    api.pipe("Task_lamp_arm",[(-1.05,.96,.10),(-1.05,1.45,.10),(-.96,1.65,.10),(-.85,1.66,.10)],
             .012,"Frame",sides=api.N(16,10,8),collision=False)
    api.lathe("Task_lamp_shade",(-.85,1.535,.10),[(0,.105),(.025,.105),(.105,.052),(.125,.027)],
              "Enamel",sides=api.N(32,20,12),collision=False)
    api.lathe("Task_lamp_diffuser",(-.85,1.526,.10),[(0,.087),(.008,.087)],
              "LampGlass",sides=api.N(28,18,12),collision=False)
    api.pipe("Task_lamp_cord",[(-1.05,.96,.13),(-1.13,.92,.25),(-1.13,.55,.30)],
             .006,"PlasticDark",sides=api.N(12,8,6),collision=False)
    _front_plate(api,"Bench_id","СЛЕСАРНЫЙ ПОСТ  02",(-.77,.77,-.358),.64,.08,size=.030)
    _front_plate(api,"Grinder_rating","220 В",(.73,1.20,-.128),.14,.045,size=.023)
    api.end()


def _radio_desk(api):
    api.begin("KH_RadioDesk", "Дежурное место радиста: стол, приёмопередатчик, блок питания, микрофон, гарнитура, журнал и кабели")
    api.box("Desktop",(0,.762,0),(1.5,.055,.75),"WornWood",bevel=.013)
    for x in (-.66,.66):
        for z in (-.28,.28):
            _cylinder(api,"Desk_leg_%s_%s"%(x,z),(x,.02,z),(x,.73,z),.023,"Frame")
            api.lathe("Leg_foot_%s_%s"%(x,z),(x,0,z),[(0,.030),(.024,.030)],"PlasticDark",sides=api.N(16,10,8),collision=False)
    for z in (-.28,.28):
        api.box("Desk_apron_%s"%z,(0,.68,z),(1.32,.12,.034),"Frame",bevel=.005)
    api.box("Desk_drawer",(-.45,.625,-.01),(.40,.13,.51),"TimberRaw",bevel=.012)
    api.box("Drawer_front",(-.45,.625,-.277),(.41,.13,.027),"Enamel",bevel=.008)
    api.pipe("Drawer_pull",[(-.52,.625,-.293),(-.52,.625,-.323),(-.38,.625,-.323),(-.38,.625,-.293)],.007,"Frame",sides=api.N(12,8,6),collision=False)
    # Case is assembled around a recessed instrument face rather than one block.
    api.box("Radio_chassis",(-.18,.94,.10),(.76,.275,.34),"Frame",bevel=.018)
    api.box("Radio_top",(-.18,1.091,.10),(.79,.025,.36),"Enamel",bevel=.007)
    api.box("Radio_front_panel",(-.18,.947,-.082),(.75,.256,.018),"Enamel",bevel=.007)
    api.box("Frequency_window_gasket",(-.26,.997,-.096),(.38,.089,.012),"PlasticDark",bevel=.004,collision=False)
    api.box("Frequency_scale",(-.26,.997,-.104),(.351,.066,.006),"Paper",bevel=.002,collision=False)
    api.box("Frequency_glass",(-.26,.997,-.109),(.350,.065,.004),"Glass",bevel=.002,collision=False)
    for j in range(api.N(17,11,7)):
        x=-.418+j*.316/(api.N(17,11,7)-1)
        api.box("Tuning_tick_%02d"%j,(x,1.003,-.113),(.0015,.026 if j%4==0 else .015,.002),"PlasticDark",bevel=.0004,collision=False)
    api.box("Tuning_pointer",(-.226,1.001,-.115),(.003,.042,.003),"SignRed",bevel=.0005,collision=False)
    for j,(x,y,r) in enumerate(((.105,.965,.047),(-.40,.885,.025),(-.25,.885,.025),(-.10,.885,.025),(.04,.885,.025))):
        _cylinder(api,"Radio_knob_ring_%s"%j,(x,y,-.094),(x,y,-.105),r*1.15,"Frame",collision=False)
        _cylinder(api,"Radio_knob_%s"%j,(x,y,-.104),(x,y,-.137),r,"PlasticDark",collision=False)
        api.box("Knob_indicator_%s"%j,(x,y+r*.55,-.139),(.004,r*.50,.003),"Paper",bevel=.0005,collision=False)
    for x in (-.536,.176):
        api.pipe("Radio_handle_%s"%x,[(x,.87,-.103),(x,.87,-.155),(x,1.035,-.155),(x,1.035,-.103)],.007,"Frame",sides=api.N(12,8,6),collision=False)
    for j in range(api.N(12,8,5)):
        x=-.48+j*.60/(api.N(12,8,5)-1)
        api.box("Radio_top_vent_%02d"%j,(x,1.105,.12),(.013,.003,.15),"PlasticDark",bevel=.002,collision=False)
    _front_plate(api,"Radio_callsign","ПОЗЫВНОЙ: КУРС",(-.17,1.062,-.099),.36,.042,size=.021)
    # Separate power pack with cable strain relief, rear mains lead and switch.
    api.box("Power_pack",(-.54,.854,.267),(.27,.13,.17),"PlasticDark",bevel=.012)
    api.pipe("Radio_DC_lead",[(-.40,.86,.27),(-.28,.82,.30),(-.16,.85,.279)],.006,"PlasticDark",sides=api.N(12,8,6),collision=False)
    api.pipe("Desk_mains_lead",[(-.64,.86,.30),(-.70,.77,.32),(-.70,.42,.31),(-.67,.05,.28)],.007,"PlasticDark",sides=api.N(12,8,6),collision=False)
    # Desk microphone on weighted base and gooseneck, with a curved coil lead.
    api.lathe("Mic_weighted_base",(-.56,.792,-.242),[(0,.069),(.025,.069),(.037,.052)],"Frame",sides=api.N(28,18,12))
    api.pipe("Mic_gooseneck",[(-.56,.823,-.242),(-.56,.96,-.242),(-.51,1.05,-.25),(-.48,1.07,-.26)],.007,"Frame",sides=api.N(14,10,8),collision=False)
    api.box("Mic_capsule",(-.463,1.075,-.266),(.067,.10,.056),"PlasticDark",bevel=.025,collision=False)
    for j in range(api.N(6,4,3)):
        api.box("Mic_grille_%s"%j,(-.463,1.045+j*.012,-.297),(.044,.004,.004),"Frame",bevel=.001,collision=False)
    coil=[]
    count=api.N(60,40,24)
    for j in range(count):
        t=j/(count-1)*pi*8
        coil.append((-.51+j/(count-1)*.27,.804+sin(t)*.008,-.225+cos(t)*.008))
    api.pipe("Microphone_coiled_lead",coil,.0035,"PlasticDark",sides=api.N(10,8,6),collision=False)
    # Open duty log, cloth cover, layered page block, annotations, and pen.
    api.box("Log_cover",(.42,.797,-.16),(.49,.010,.32),"CanvasDark",bevel=.008,collision=False)
    for j in range(api.N(6,4,2)):
        api.box("Log_page_%s"%j,(.42,.804+j*.0014,-.16),(.471,.0012,.302),"Paper",bevel=.001,collision=False)
    api.box("Log_spine",(.42,.817,-.16),(.009,.007,.30),"TimberRaw",bevel=.002,collision=False)
    # Writing stands vertically on the fore-edge nameplate; page marks are geometry.
    for side in (-1,1):
        for j in range(api.N(7,5,3)):
            api.box("Log_written_line_%s_%s"%(side,j),(.42+side*.115,.816,-.26+j*.033),(.15-(j%3)*.021,.0012,.0015),"Lettering",bevel=.0003,collision=False)
    api.pipe("Log_pen",[(.54,.822,-.25),(.66,.822,-.11)],.004,"Frame",sides=api.N(12,8,6),collision=False)
    api.box("Paper_message",(.49,.795,.192),(.29,.0015,.16),"Paper",bevel=.002,collision=False)
    api.box("Antenna_base",(.12,1.13,.245),(.06,.055,.06),"Frame",bevel=.008,collision=False)
    api.pipe("Radio_short_antenna",[(.12,1.15,.245),(.20,1.24,.29)],.0035,"Frame",sides=api.N(10,8,6),collision=False)
    _front_plate(api,"Duty_log_label","ЖУРНАЛ ДЕЖУРСТВ",(.43,.684,-.305),.47,.06,size=.024)
    api.end()


def build(api):
    """Create six independently exportable, reusable props at local floor origin."""
    _diesel_generator(api)
    _power_switchboard(api)
    _water_treatment(api)
    _utility_rack(api)
    _repair_bench(api)
    _radio_desk(api)
