#!/usr/bin/env python3
"""
Generates the PMM_VIPCore panorama: pmm_vip.xml + pmm_vip.css.

    python3 gen_layout.py <addon root>      (writes <root>/panorama/...)

The plugin (Paint.cs) drives ids and class names from the constants below, keep them in sync:
  tiles  vt-0..vt-11   (3 x 4 grid, 12 per page)
  chips  vp-0..vp-15   (value picker, 4 x 4)
  rows   vl-0..vl-7    (sub menu list)
  dots   vip-dot-0..7
"""
import pathlib, re, sys

TILES, COLS = 12, 3
CHIPS, CCOLS = 16, 4
ROWS = 8
DOTS = 8

# icon class name -> game image (all present in the CS2 client VPK)
ICONS = {
    # equipment
    "armor_helmet": "equipment/armor_helmet", "armor": "equipment/armor", "kevlar": "equipment/kevlar",
    "helmet": "equipment/helmet", "healthshot": "equipment/healthshot", "defuser": "equipment/defuser",
    "flashbang": "equipment/flashbang", "smokegrenade": "equipment/smokegrenade", "hegrenade": "equipment/hegrenade",
    "molotov": "equipment/molotov", "decoy": "equipment/decoy", "grenadepack": "equipment/grenadepack",
    "taser": "equipment/taser", "ak47": "equipment/ak47", "awp": "equipment/awp", "deagle": "equipment/deagle",
    "knife": "equipment/knife", "ammobox": "equipment/ammobox", "c4": "equipment/c4",
    "planted_c4": "equipment/planted_c4", "assaultsuit": "equipment/assaultsuit", "customplayer": "equipment/customplayer",
    # ui
    "health": "ui/health", "shield": "ui/shield", "fast": "ui/fast", "exojump": "ui/exojump",
    "parachute": "ui/parachute", "dollar_sign": "ui/dollar_sign", "coin_stack": "ui/coin_stack",
    "star": "ui/star", "gift": "ui/gift", "random": "ui/random", "clantag": "ui/clantag", "zoom_in": "ui/zoom_in",
    "camera": "ui/camera", "bullet": "ui/bullet", "bullet_burst": "ui/bullet_burst", "kill": "ui/kill",
    "kill_headshot": "ui/kill_headshot", "refresh": "ui/refresh", "undo": "ui/undo", "timer": "ui/timer",
    "hourglass": "ui/hourglass", "stats": "ui/stats", "key": "ui/key", "teamplayer": "ui/teamplayer",
    "colorwheel": "ui/colorwheel", "loadout": "ui/loadout", "inventory": "ui/inventory", "xp_rank": "ui/xp_rank",
    "sound_3": "ui/sound_3", "crosshair": "ui/crosshair", "bomb": "ui/bomb", "buyzone": "ui/buyzone",
    "shoppingcart": "ui/shoppingcart", "leader": "ui/leader", "trophy": "ui/trophy", "settings": "ui/settings",
    "clock": "ui/clock", "lock": "ui/locked", "check": "ui/check", "warning": "ui/warning",
}

# group palette: name -> (accent, light, dark)  (accent is the main colour)
PALETTE = {
    "gold":     ("#f2c35b", "#ffe39a", "#a8701a"),
    "silver":   ("#c9d2dc", "#f2f6fa", "#7d8894"),
    "bronze":   ("#d8915a", "#f3c39b", "#8a4f22"),
    "platinum": ("#9fe3e0", "#e2fbfa", "#4f9a97"),
    "emerald":  ("#5fd38d", "#b7f5cf", "#24804a"),
    "ruby":     ("#ef5a6f", "#ffb0bb", "#9a2133"),
    "sapphire": ("#5f9cf2", "#b9d6ff", "#2a5aa8"),
    "amethyst": ("#b07cf2", "#ddc4ff", "#6a3bb0"),
}

T = "\t"


def xml() -> str:
    o = []
    a = o.append
    a('<root>')
    a(T + '<styles>')
    a(T * 2 + '<include src="s2r://panorama/styles/csgostyles.vcss_c" />')
    a(T * 2 + '<include src="s2r://panorama/styles/custom_game/pmm_vip.vcss_c" />')
    a(T * 2 + '<include src="s2r://panorama/styles/custom_game/pmm_vip_theme.vcss_c" />')
    a(T + '</styles>')
    a(T + '<Panel class="vip-screen" hittest="false">')
    a(T * 2 + '<Panel id="vip-root" class="vip-root vip-hidden view-main grp-gold">')
    a(T * 3 + '<Panel class="vip-glow" hittest="false" />')
    # header
    a(T * 3 + '<Panel class="vip-head">')
    a(T * 4 + '<Panel class="vip-badge" hittest="false"><Label class="vip-badge-t stratum-bold-tf" text="{s:vip_brand}" /></Panel>')
    a(T * 4 + '<Panel class="vip-ht" hittest="false">')
    a(T * 5 + '<Label class="vip-nick stratum-bold-tf" text="{s:vip_nick}" />')
    a(T * 5 + '<Panel class="vip-meta" hittest="false">')
    a(T * 6 + '<Panel class="vip-group" hittest="false"><Label class="vip-group-t stratum-bold-tf" text="{s:vip_group}" /></Panel>')
    a(T * 6 + '<Panel class="vip-clock" hittest="false" />')
    a(T * 6 + '<Label class="vip-exp-pre stratum-medium-tf" text="{s:vip_exp_pre}" />')
    a(T * 6 + '<Label class="vip-exp-date stratum-bold-tf" text="{s:vip_exp_date}" />')
    a(T * 6 + '<Label class="vip-exp-left stratum-medium-tf" text="{s:vip_exp_left}" />')
    a(T * 5 + '</Panel>')
    a(T * 4 + '</Panel>')
    a(T * 4 + '<Button id="vip-close" class="vip-x"><Label class="vip-x-t stratum-bold-tf" text="×" /></Button>')
    a(T * 3 + '</Panel>')
    a(T * 3 + '<Panel class="vip-sep" hittest="false" />')
    # sub title (pick + list views)
    a(T * 3 + '<Panel class="vip-sub only-sub" hittest="false">')
    a(T * 4 + '<Panel class="vip-sub-icbox" hittest="false"><Panel id="vip-sub-ic" class="vt-ic ic-star" hittest="false" /></Panel>')
    a(T * 4 + '<Panel class="vip-sub-text" hittest="false">')
    a(T * 5 + '<Label class="vip-sub-title stratum-bold-tf" text="{s:vip_sub_title}" />')
    a(T * 5 + '<Label class="vip-sub-hint stratum-medium-tf" text="{s:vip_sub_hint}" />')
    a(T * 4 + '</Panel>')
    a(T * 3 + '</Panel>')
    # main grid
    a(T * 3 + '<Panel class="vip-grid only-main">')
    for r in range(TILES // COLS):
        a(T * 4 + f'<Panel id="vip-gr-{r}" class="vip-grow">')
        for c in range(COLS):
            i = r * COLS + c
            a(T * 5 + f'<Button id="vt-{i}" class="vt k-toggle">')
            a(T * 6 + f'<Panel class="vt-icbox" hittest="false"><Panel id="vt-ic-{i}" class="vt-ic ic-star" hittest="false" /></Panel>')
            a(T * 6 + '<Panel class="vt-text" hittest="false">')
            a(T * 7 + f'<Label class="vt-name stratum-bold-tf" text="{{s:vtn{i}}}" />')
            a(T * 7 + f'<Label class="vt-desc stratum-medium-tf" text="{{s:vts{i}}}" />')
            a(T * 6 + '</Panel>')
            a(T * 6 + '<Panel class="vt-sw" hittest="false"><Panel class="vt-knob" hittest="false" /></Panel>')
            a(T * 6 + f'<Panel class="vt-chip" hittest="false"><Label class="vt-chip-t stratum-bold-tf" text="{{s:vtv{i}}}" /><Panel class="vt-chev" hittest="false" /></Panel>')
            a(T * 6 + '<Panel class="vt-act" hittest="false"><Label class="vt-act-t stratum-bold-tf" text="{s:vip_use}" /></Panel>')
            a(T * 6 + '<Panel class="vt-lock" hittest="false" />')
            a(T * 5 + '</Button>')
        a(T * 4 + '</Panel>')
    a(T * 3 + '</Panel>')
    # picker
    a(T * 3 + '<Panel class="vip-chips only-pick">')
    for r in range(CHIPS // CCOLS):
        a(T * 4 + f'<Panel id="vip-cr-{r}" class="vip-crow">')
        for c in range(CCOLS):
            i = r * CCOLS + c
            a(T * 5 + f'<Button id="vp-{i}" class="vp"><Label class="vp-t stratum-bold-tf" text="{{s:vpc{i}}}" /></Button>')
        a(T * 4 + '</Panel>')
    a(T * 3 + '</Panel>')
    # list
    a(T * 3 + '<Panel class="vip-list only-list">')
    for i in range(ROWS):
        a(T * 4 + f'<Button id="vl-{i}" class="vl"><Label class="vl-t stratum-bold-tf" text="{{s:vlr{i}}}" /><Panel class="vl-chev" hittest="false" /></Button>')
    a(T * 3 + '</Panel>')
    # footer
    a(T * 3 + '<Panel class="vip-foot">')
    a(T * 4 + '<Panel class="vip-fleft" hittest="false">')
    a(T * 5 + '<Button id="vip-back" class="vip-back"><Label class="vip-back-t stratum-bold-tf" text="{s:vip_back}" /></Button>')
    a(T * 5 + '<Panel id="vip-toast" class="vip-toast" hittest="false"><Panel class="vip-toast-ic" hittest="false" /><Label class="vip-toast-t stratum-medium-tf" text="{s:vip_toast}" /></Panel>')
    a(T * 4 + '</Panel>')
    a(T * 4 + '<Panel class="vip-pager" hittest="false">')
    a(T * 5 + '<Panel class="vip-pager-in" hittest="false">')
    a(T * 5 + '<Button id="vip-prev" class="vip-pb"><Panel class="vip-pb-ic vip-pb-l" hittest="false" /></Button>')
    a(T * 5 + '<Label class="vip-page stratum-bold-tf" text="{s:vip_page}" />')
    a(T * 5 + '<Panel class="vip-dots" hittest="false">')
    for i in range(DOTS):
        a(T * 6 + f'<Panel id="vip-dot-{i}" class="vip-dot" hittest="false" />')
    a(T * 5 + '</Panel>')
    a(T * 5 + '<Button id="vip-next" class="vip-pb"><Panel class="vip-pb-ic" hittest="false" /></Button>')
    a(T * 5 + '</Panel>')
    a(T * 4 + '</Panel>')
    a(T * 4 + '<Panel class="vip-fright" hittest="false" />')
    a(T * 3 + '</Panel>')
    a(T * 2 + '</Panel>')
    a(T + '</Panel>')
    a('</root>')
    return "\n".join(o) + "\n"


BASE = r"""/* ============================================================
   PMM_VIPCore - VIP menu panorama (generated by tools/gen_layout.py)
   Server drives: dialog variables on #vip-root, classes per player.
   Root classes: vip-hidden, view-main | view-pick | view-list, grp-<palette>,
                 pos-left | pos-right, has-back, has-pages, toast-on, toast-warn
   Tile classes: k-toggle | k-select | k-action | k-plain, on, locked, slot-off
   ============================================================ */

@define dur: 0.14s;

.vip-screen
{
	width: 100%;
	height: 100%;
	z-index: 2000000000;
}

.vip-root
{
	width: 790px;
	height: fit-children;
	flow-children: down;
	horizontal-align: center;
	vertical-align: middle;
	background-color: gradient( linear, 0% 0%, 0% 100%, from( #19171cf5 ), to( #0d0c10f5 ) );
	border: 1px solid #3a2f1c;
	border-radius: 14px;
	box-shadow: #000000c0 0px 18px 50px 0px;
	overflow: clip clip;
}

.vip-root.vip-hidden { visibility: collapse; }
.vip-root.pos-left { horizontal-align: left; margin-left: 40px; }
.vip-root.pos-right { horizontal-align: right; margin-right: 40px; }

.vip-glow
{
	width: 100%;
	height: 3px;
}

/* ---------- views ---------- */

.only-main, .only-pick, .only-list, .only-sub { visibility: collapse; }
.vip-root.view-main .only-main { visibility: visible; }
.vip-root.view-pick .only-pick { visibility: visible; }
.vip-root.view-list .only-list { visibility: visible; }
.vip-root.view-pick .only-sub, .vip-root.view-list .only-sub { visibility: visible; }

.slot-off { visibility: collapse; }

/* ---------- header ---------- */

.vip-head
{
	width: 100%;
	height: 92px;
	flow-children: right;
	padding: 17px 20px 16px 20px;
	background-color: gradient( linear, 0% 0%, 100% 0%, from( #f2c35b14 ), to( #00000000 ) );
}

.vip-badge
{
	width: 58px;
	height: 58px;
	border-radius: 12px;
	vertical-align: center;
}

.vip-badge-t
{
	horizontal-align: center;
	vertical-align: center;
	font-size: 22px;
	letter-spacing: 1px;
	color: #2a1d07;
}

.vip-ht
{
	width: fill-parent-flow( 1.0 );
	height: fit-children;
	flow-children: down;
	vertical-align: center;
	margin-left: 16px;
}

.vip-nick
{
	width: 100%;
	font-size: 23px;
	color: #ffffff;
	letter-spacing: 0.5px;
	text-overflow: shrink;
}

.vip-meta
{
	height: fit-children;
	flow-children: right;
	margin-top: 5px;
}

.vip-group
{
	height: 22px;
	padding: 0px 10px;
	border-radius: 5px;
	vertical-align: center;
}

.vip-group-t
{
	vertical-align: center;
	font-size: 13px;
	letter-spacing: 1.5px;
	color: #24190a;
	text-transform: uppercase;
}

.vip-clock
{
	width: 15px;
	height: 15px;
	margin-left: 12px;
	vertical-align: center;
	background-image: url("s2r://panorama/images/icons/ui/clock.vsvg");
	background-size: 100% 100%;
	wash-color: #a7a29a;
}

.vip-exp-pre, .vip-exp-date, .vip-exp-left
{
	vertical-align: center;
	font-size: 14px;
	color: #a7a29a;
	margin-left: 6px;
}

.vip-exp-date { margin-left: 4px; }

.vip-x
{
	width: 36px;
	height: 36px;
	vertical-align: center;
	border-radius: 8px;
	background-color: #ffffff0d;
	transition-property: background-color, brightness;
	transition-duration: dur;
	transition-timing-function: ease-out;
}

.vip-x:hover { background-color: #c93642; }

.vip-x-t
{
	horizontal-align: center;
	vertical-align: center;
	font-size: 24px;
	color: #ffffffcc;
}

.vip-sep
{
	width: 100%;
	height: 1px;
	margin: 0px 20px;
	background-color: gradient( linear, 0% 0%, 100% 0%, from( #ffffff00 ), color-stop( 0.5, #ffffff1a ), to( #ffffff00 ) );
}

/* ---------- sub title ---------- */

.vip-sub
{
	width: 100%;
	height: 72px;
	flow-children: right;
	padding: 14px 20px 4px 20px;
}

.vip-sub-icbox
{
	width: 46px;
	height: 46px;
	border-radius: 10px;
	vertical-align: center;
}

.vip-sub-text
{
	width: fill-parent-flow( 1.0 );
	height: fit-children;
	flow-children: down;
	vertical-align: center;
	margin-left: 12px;
}

.vip-sub-title
{
	width: 100%;
	font-size: 18px;
	color: #ece8e0;
	text-transform: uppercase;
	letter-spacing: 0.6px;
	text-overflow: shrink;
}

.vip-sub-hint
{
	font-size: 13px;
	color: #8e897f;
	margin-top: 3px;
}

.vip-root.view-list .vip-sub-icbox { visibility: collapse; }
.vip-root.view-list .vip-sub-text { margin-left: 0px; }

/* ---------- tiles ---------- */

.vip-grid
{
	width: 100%;
	height: fit-children;
	flow-children: down;
	padding: 16px 20px 6px 20px;
}

.vip-grow
{
	width: 100%;
	height: 88px;
	flow-children: right;
}

.vt
{
	width: 243px;
	height: 78px;
	margin-right: 10px;
	flow-children: right;
	border-radius: 10px;
	background-color: #ffffff08;
	border: 1px solid #ffffff12;
	transition-property: background-color, border, brightness;
	transition-duration: dur;
	transition-timing-function: ease-out;
}

.vt:last-child { margin-right: 0px; }
.vt:hover { background-color: #ffffff12; }

.vt-icbox
{
	width: 46px;
	height: 46px;
	margin-left: 12px;
	vertical-align: center;
	border-radius: 10px;
	background-color: #ffffff0d;
}

.vt-ic
{
	width: 26px;
	height: 26px;
	horizontal-align: center;
	vertical-align: center;
	background-size: 100% 100%;
	background-repeat: no-repeat;
	wash-color: #d8d4cc;
}

.vt-text
{
	width: fill-parent-flow( 1.0 );
	height: fit-children;
	flow-children: down;
	vertical-align: center;
	margin-left: 12px;
	margin-right: 8px;
}

.vt-name
{
	width: 100%;
	font-size: 15px;
	color: #ece8e0;
	text-transform: uppercase;
	letter-spacing: 0.6px;
	text-overflow: shrink;
	white-space: nowrap;
}

.vt-desc
{
	width: 100%;
	font-size: 12px;
	color: #8e897f;
	margin-top: 3px;
	text-overflow: ellipsis;
	white-space: nowrap;
}

/* controls: one per kind */
.vt-sw, .vt-chip, .vt-act, .vt-lock { visibility: collapse; }
.vt.k-toggle .vt-sw { visibility: visible; }
.vt.k-select .vt-chip { visibility: visible; }
.vt.k-action .vt-act { visibility: visible; }
.vt.locked .vt-sw, .vt.locked .vt-chip, .vt.locked .vt-act { visibility: collapse; }
.vt.locked .vt-lock { visibility: visible; }
.vt.locked { opacity: 0.42; }

.vt-sw
{
	width: 42px;
	height: 24px;
	margin-right: 12px;
	vertical-align: center;
	border-radius: 12px;
	background-color: #ffffff1f;
	transition-property: background-color, box-shadow;
	transition-duration: dur;
	transition-timing-function: ease-out;
}

.vt-knob
{
	width: 18px;
	height: 18px;
	margin-left: 3px;
	horizontal-align: left;
	vertical-align: center;
	border-radius: 9px;
	background-color: #9a958c;
	transition-property: horizontal-align, margin-left, background-color;
	transition-duration: dur;
	transition-timing-function: ease-out;
}

.vt.on .vt-knob
{
	horizontal-align: right;
	margin-left: 0px;
	margin-right: 3px;
	background-color: #ffffff;
}

.vt-chip
{
	height: 28px;
	max-width: 96px;
	margin-right: 12px;
	padding: 0px 6px 0px 9px;
	vertical-align: center;
	flow-children: right;
	border-radius: 7px;
	border: 1px solid #ffffff33;
}

.vt-chip-t
{
	max-width: 70px;
	vertical-align: center;
	font-size: 14px;
	text-overflow: shrink;
	white-space: nowrap;
}

.vt-chev
{
	width: 12px;
	height: 12px;
	margin-left: 4px;
	vertical-align: center;
	background-image: url("s2r://panorama/images/icons/ui/expand.vsvg");
	background-size: 100% 100%;
}

.vt-act
{
	height: 28px;
	margin-right: 12px;
	padding: 0px 9px;
	vertical-align: center;
	border-radius: 7px;
}

.vt-act-t
{
	vertical-align: center;
	font-size: 12px;
	letter-spacing: 1px;
	text-transform: uppercase;
}

.vt-lock
{
	width: 16px;
	height: 16px;
	margin-right: 14px;
	vertical-align: center;
	background-image: url("s2r://panorama/images/icons/ui/locked.vsvg");
	background-size: 100% 100%;
	wash-color: #bbbbbb;
}

/* plain row inside the grid (unknown VIP row) */
.vt.k-plain .vt-desc { visibility: collapse; }

/* ---------- picker chips ---------- */

.vip-chips
{
	width: 100%;
	height: fit-children;
	flow-children: down;
	padding: 10px 20px 4px 20px;
}

.vip-crow
{
	width: 100%;
	height: 60px;
	flow-children: right;
}

.vp
{
	width: 180px;
	height: 50px;
	margin-right: 10px;
	border-radius: 9px;
	background-color: #ffffff08;
	border: 1px solid #ffffff14;
	transition-property: background-color, border, box-shadow;
	transition-duration: dur;
	transition-timing-function: ease-out;
}

.vp:last-child { margin-right: 0px; }
.vp:hover { background-color: #ffffff12; }

.vp-t
{
	width: 90%;
	horizontal-align: center;
	vertical-align: center;
	text-align: center;
	font-size: 16px;
	color: #e6e2da;
	text-overflow: shrink;
	white-space: nowrap;
}

/* ---------- list rows ---------- */

.vip-list
{
	width: 100%;
	height: fit-children;
	flow-children: down;
	padding: 10px 20px 4px 20px;
}

.vl
{
	width: 100%;
	height: 48px;
	margin-bottom: 8px;
	flow-children: right;
	border-radius: 9px;
	background-color: #ffffff08;
	border: 1px solid #ffffff12;
	transition-property: background-color, border;
	transition-duration: dur;
	transition-timing-function: ease-out;
}

.vl:hover { background-color: #ffffff12; }

.vl-t
{
	width: fill-parent-flow( 1.0 );
	margin-left: 16px;
	vertical-align: center;
	font-size: 16px;
	color: #ece8e0;
	text-overflow: shrink;
	white-space: nowrap;
}

.vl-chev
{
	width: 14px;
	height: 14px;
	margin-right: 16px;
	vertical-align: center;
	background-image: url("s2r://panorama/images/icons/ui/expand.vsvg");
	background-size: 100% 100%;
	transform: rotateZ( -90deg );
	wash-color: #8e897f;
}

.vl.dis { opacity: 0.42; }

/* ---------- footer ---------- */

.vip-foot
{
	width: 100%;
	height: 52px;
	flow-children: right;
	padding: 4px 20px 14px 20px;
}

.vip-fleft, .vip-fright
{
	width: 250px;
	height: 100%;
	flow-children: right;
}

.vip-back
{
	height: 32px;
	padding: 0px 12px;
	vertical-align: center;
	margin-right: 10px;
	border-radius: 7px;
	background-color: #ffffff0d;
	visibility: collapse;
	transition-property: background-color;
	transition-duration: dur;
	transition-timing-function: ease-out;
}

.vip-root.has-back .vip-back { visibility: visible; }
.vip-back:hover { background-color: #ffffff1c; }

.vip-back-t
{
	vertical-align: center;
	font-size: 12px;
	letter-spacing: 1px;
	color: #dddddd;
	text-transform: uppercase;
}

.vip-toast
{
	height: 32px;
	max-width: 250px;
	vertical-align: center;
	padding: 0px 10px;
	flow-children: right;
	border-radius: 8px;
	background-color: #1f2a1b;
	border: 1px solid #6fbf5a66;
	opacity: 0;
	transition-property: opacity;
	transition-duration: 0.2s;
	transition-timing-function: ease-out;
}

.vip-root.toast-on .vip-toast { opacity: 1; }
.vip-root.toast-warn .vip-toast { background-color: #2a1b1b; border: 1px solid #d9606066; }

.vip-toast-ic
{
	width: 13px;
	height: 13px;
	vertical-align: center;
	background-image: url("s2r://panorama/images/icons/ui/check.vsvg");
	background-size: 100% 100%;
	wash-color: #8fe07a;
}

.vip-root.toast-warn .vip-toast-ic { background-image: url("s2r://panorama/images/icons/ui/cancel.vsvg"); wash-color: #ff8a8a; }

.vip-toast-t
{
	max-width: 210px;
	margin-left: 7px;
	vertical-align: center;
	font-size: 13px;
	color: #d6efcd;
	text-overflow: shrink;
	white-space: nowrap;
}

.vip-root.toast-warn .vip-toast-t { color: #f3d0d0; }

.vip-pager
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
}

.vip-pager-in
{
	height: 100%;
	horizontal-align: center;
	flow-children: right;
	opacity: 0;
}

.vip-root.has-pages .vip-pager-in { opacity: 1; }

.vip-pb
{
	width: 32px;
	height: 30px;
	vertical-align: center;
	border-radius: 7px;
	background-color: #ffffff0f;
	transition-property: background-color;
	transition-duration: dur;
	transition-timing-function: ease-out;
}

.vip-pb:hover { background-color: #ffffff22; }

.vip-pb-ic
{
	width: 12px;
	height: 12px;
	horizontal-align: center;
	vertical-align: center;
	background-image: url("s2r://panorama/images/icons/ui/expand.vsvg");
	background-size: 100% 100%;
	transform: rotateZ( -90deg );
	wash-color: #eeeeee;
}

.vip-pb-l { transform: rotateZ( 90deg ); }

.vip-page
{
	vertical-align: center;
	margin: 0px 10px 0px 12px;
	font-size: 15px;
	color: #cfcac0;
}

.vip-dots
{
	height: 4px;
	vertical-align: center;
	flow-children: right;
	margin-right: 12px;
}

.vip-dot
{
	width: 16px;
	height: 4px;
	margin-right: 4px;
	border-radius: 2px;
	background-color: #ffffff26;
}
"""


def accent_rules(name, acc, light, dark, default=False) -> str:
    sel = ".vip-root" if default else f".vip-root.grp-{name}"
    a, l, d = acc, light, dark
    rules = [
        (f"{sel} .vip-glow", f"background-color: gradient( linear, 0% 0%, 100% 0%, from( {a}00 ), color-stop( 0.3, {a} ), color-stop( 0.5, {l} ), color-stop( 0.7, {a} ), to( {a}00 ) );"),
        (f"{sel} .vip-badge", f"background-color: gradient( linear, 0% 0%, 100% 100%, from( {l} ), color-stop( 0.6, {a} ), to( {d} ) ); box-shadow: {a}55 0px 0px 18px 0px;"),
        (f"{sel} .vip-group", f"background-color: gradient( linear, 0% 0%, 100% 0%, from( {a} ), to( {d} ) );"),
        (f"{sel} .vip-exp-date", f"color: {a};"),
        (f"{sel} .vip-sub-icbox", f"background-color: {a}26;"),
        (f"{sel} .vip-sub-icbox .vt-ic", f"wash-color: {a};"),
        (f"{sel} .vt:hover", f"border: 1px solid {a}99;"),
        (f"{sel} .vt.on .vt-icbox", f"background-color: {a}26;"),
        (f"{sel} .vt.on .vt-ic", f"wash-color: {a};"),
        (f"{sel} .vt.on .vt-desc", f"color: {l}bb;"),
        (f"{sel} .vt.on .vt-sw", f"background-color: gradient( linear, 0% 0%, 100% 0%, from( {d} ), to( {a} ) ); box-shadow: {a}66 0px 0px 10px 0px;"),
        (f"{sel} .vt-chip", f"border: 1px solid {a}88;"),
        (f"{sel} .vt-chip-t", f"color: {a};"),
        (f"{sel} .vt-chev", f"wash-color: {a};"),
        (f"{sel} .vt-act", f"background-color: {a}1f;"),
        (f"{sel} .vt-act-t", f"color: {a};"),
        (f"{sel} .vp:hover", f"border: 1px solid {a}99;"),
        (f"{sel} .vp.sel", f"background-color: gradient( linear, 0% 0%, 100% 100%, from( {a}33 ), to( {a}14 ) ); border: 1px solid {a}; box-shadow: {a}40 0px 0px 12px 0px;"),
        (f"{sel} .vp.sel .vp-t", f"color: {l};"),
        (f"{sel} .vl:hover", f"border: 1px solid {a}99;"),
        (f"{sel} .vip-dot.a", f"background-color: {a};"),
        (f"{sel} .vip-head", f"background-color: gradient( linear, 0% 0%, 100% 0%, from( {a}14 ), to( {a}00 ) );"),
    ]
    return "\n".join(f"{s} {{ {b} }}" for s, b in rules) + "\n"


def css() -> str:
    o = [BASE, "\n/* ---------- group palettes (grp-*) ---------- */\n"]
    g = PALETTE["gold"]
    o.append("/* default = gold */\n" + accent_rules("gold", *g, default=True))
    for name, (a, l, d) in PALETTE.items():
        o.append(f"\n/* {name} */\n" + accent_rules(name, a, l, d))
    o.append("\n/* ---------- icons (ic-*) ---------- */\n")
    for name, path in ICONS.items():
        o.append(f'.ic-{name} {{ background-image: url("s2r://panorama/images/icons/{path}.vsvg"); }}\n')
    return "".join(o)


def theme_css(project: pathlib.Path) -> str:
    """pmm_vip_theme.css with the defaults of Config.cs - the same template the plugin fills from its config."""
    tpl = (project / "theme.template.css").read_text(encoding="utf-8").replace("\r\n", "\n")
    cfg = (project / "Config.cs").read_text(encoding="utf-8")
    theme = cfg[cfg.index("public class ThemeOptions"):cfg.index("public class PaletteColors")]
    colors = dict(re.findall(r'public string (\w+) \{ get; set; \} = "(#[0-9a-fA-F]+)"', theme))
    palettes = re.findall(r'\["(\w+)"\] = new\("(#\w+)", "(#\w+)", "(#\w+)", "(#\w+)"\)', theme)
    mark = "/*@@palette@@*/\n"
    head, pal = tpl.split(mark, 1)
    for k, v in colors.items():
        v = v.lower()
        head = head.replace("{{%s.rgb}}" % k, v[:7]).replace("{{%s}}" % k, v)
    out = [head]
    for name, a_, l_, d_, on in palettes:
        out.append(pal.replace("{{name}}", name.lower()).replace("{{a}}", a_.lower()[:7]).replace("{{l}}", l_.lower()[:7])
                   .replace("{{d}}", d_.lower()[:7]).replace("{{on}}", on.lower()))
    left = re.findall(r"\{\{[^}]+\}\}", "".join(out))
    if left:
        raise SystemExit(f"unfilled placeholders: {sorted(set(left))}")
    return "".join(out)


def main():
    root = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else ".")
    lay = root / "panorama" / "layout" / "custom_game"
    sty = root / "panorama" / "styles" / "custom_game"
    lay.mkdir(parents=True, exist_ok=True)
    sty.mkdir(parents=True, exist_ok=True)
    (lay / "pmm_vip.xml").write_text(xml(), encoding="utf-8")
    (sty / "pmm_vip.css").write_text(css(), encoding="utf-8")
    project = pathlib.Path(__file__).resolve().parent.parent / "PMM_VIPCore"
    (sty / "pmm_vip_theme.css").write_text(theme_css(project), encoding="utf-8")
    print("icons:", " ".join(ICONS))
    print("palettes:", " ".join(PALETTE))


if __name__ == "__main__":
    main()
