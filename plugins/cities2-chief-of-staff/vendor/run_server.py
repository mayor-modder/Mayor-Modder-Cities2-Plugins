from __future__ import annotations

import sys
from pathlib import Path

VENDOR_ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(VENDOR_ROOT))

from chief_of_staff.mcp_server import main  # noqa: E402


if __name__ == "__main__":
    raise SystemExit(main())
