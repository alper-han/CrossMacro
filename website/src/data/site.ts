export const site = {
  name: 'CrossMacro',
  origin: 'https://alper-han.github.io',
  basePath: '/CrossMacro',
  repository: 'https://github.com/alper-han/CrossMacro',
  releases: 'https://github.com/alper-han/CrossMacro/releases/latest',
  docs: 'https://github.com/alper-han/CrossMacro/tree/dev/docs',
  description: 'Free, open-source mouse and keyboard macro recorder for Linux Wayland/X11, Windows, and macOS. Record, edit, replay, and schedule desktop tasks.',
} as const;

export function sitePath(route = '/'): string {
  return `${site.basePath}${route.startsWith('/') ? route : `/${route}`}`;
}

export function absoluteUrl(route = '/'): string {
  return new URL(sitePath(route), site.origin).toString();
}

export const guides = {
  wayland: {
    route: '/guides/linux-wayland-macro-recorder/',
    title: 'Linux Wayland Macro Recorder: Setup & First Macro | CrossMacro',
    label: 'Linux & Wayland macros',
    heading: 'Record mouse and keyboard macros on Linux Wayland.',
    description: 'Set up CrossMacro on Linux Wayland or X11, record your first mouse and keyboard macro, and understand package permissions and compositor limits.',
    summary: 'A practical guide to installing CrossMacro, recording a small task, and replaying it on your Linux desktop—with the setup details that Wayland needs.',
  },
  textExpansion: {
    route: '/guides/text-expansion/',
    title: 'Text Expansion for Linux, Windows & macOS | CrossMacro',
    label: 'Text expansion',
    heading: 'Type a shortcut. Expand the text you use every day.',
    description: 'Use CrossMacro as a text expander on Linux, Windows, and macOS. Create text shortcuts, enable expansion, and choose paste or direct typing.',
    summary: 'Replace short triggers with email addresses, replies, and reusable snippets. Learn how to set up text expansion and choose the right insertion mode.',
  },
  scheduling: {
    route: '/guides/schedule-macros/',
    title: 'Schedule Mouse & Keyboard Macros | CrossMacro',
    label: 'Scheduled desktop automation',
    heading: 'Schedule repetitive desktop tasks with macros.',
    description: 'Schedule mouse and keyboard macros with CrossMacro: one-time tasks, intervals, and weekly routines, with active-session requirements and safe examples.',
    summary: 'Turn a tested macro into a scheduled desktop task. Choose when it runs, understand what must stay open, and keep automation predictable.',
  },
} as const;

export type Guide = (typeof guides)[keyof typeof guides];
