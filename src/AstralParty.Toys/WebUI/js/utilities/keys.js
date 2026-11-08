// utilities/keys.js - Virtual-key groups and labels for the speedhack config editor
(function () {
  'use strict';
  const VK_GROUPS = [
    ['修饰键', [['VK_CONTROL', 'Ctrl'], ['VK_SHIFT', 'Shift'], ['VK_MENU', 'Alt'], ['VK_LWIN', 'Win'],
      ['VK_LCONTROL', '左Ctrl'], ['VK_RCONTROL', '右Ctrl'], ['VK_LSHIFT', '左Shift'], ['VK_RSHIFT', '右Shift'],
      ['VK_LMENU', '左Alt'], ['VK_RMENU', '右Alt']]],
    ['字母', 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'.split('').map(c => ['VK_' + c, c])],
    ['数字', Array.from({ length: 10 }, (_, i) => ['VK_' + i, String(i)])],
    ['功能键', Array.from({ length: 12 }, (_, i) => ['VK_F' + (i + 1), 'F' + (i + 1)])],
    ['常用', [['VK_SPACE', '空格'], ['VK_TAB', 'Tab'], ['VK_RETURN', '回车'], ['VK_ESCAPE', 'Esc'],
      ['VK_BACK', '退格'], ['VK_DELETE', 'Del'], ['VK_INSERT', 'Ins'], ['VK_HOME', 'Home'], ['VK_END', 'End'],
      ['VK_PRIOR', 'PgUp'], ['VK_NEXT', 'PgDn'], ['VK_UP', '↑'], ['VK_DOWN', '↓'], ['VK_LEFT', '←'], ['VK_RIGHT', '→']]]
  ];
  const VK_LABELS = Object.fromEntries(VK_GROUPS.flatMap(([, list]) => list));
  window.UtilitiesKeys = { VK_GROUPS, VK_LABELS };
})();
