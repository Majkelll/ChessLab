#!/usr/bin/env bash
set -euo pipefail

dotnet_run() { mise exec dotnet@10.0.400 -- dotnet "$@"; }

repository="$(git rev-parse --show-toplevel)"
work="${RUNNER_TEMP:-$(mktemp -d)}/engine-main"
pr_arena="$repository/tools/ChessLab.Arena/bin/Release/net10.0/ChessLab.Arena.dll"
base="${GITHUB_BASE_REF:-main}"

dotnet_run "$pr_arena" bench --mode all --check --best-of 3 --min-time-ms 1000 --output "$work-pr-bench.txt"

git fetch --no-tags --depth=1 origin "$base"
rm -rf "$work"
git worktree add --detach "$work" FETCH_HEAD
trap 'git -C "$repository" worktree remove --force "$work"' EXIT

if [ ! -d "$work/tools/ChessLab.Arena" ]; then
  echo "$base has no arena yet, so there is nothing to compare nodes per second and tactics with."
  exit 0
fi

dotnet_run build "$work/tools/ChessLab.Arena" -c Release
main_arena="$work/tools/ChessLab.Arena/bin/Release/net10.0/ChessLab.Arena.dll"

(cd "$work" && dotnet_run "$main_arena" bench --mode all --best-of 3 --min-time-ms 1000 --output "$work-main-bench.txt")
dotnet_run "$pr_arena" bench-compare --main "$work-main-bench.txt" --candidate "$work-pr-bench.txt" --max-slowdown 10

(cd "$work" && dotnet_run "$main_arena" tactics --mode all --player engine --solved "$work-main-solved.txt")
dotnet_run "$pr_arena" tactics --mode all --player engine --compare "$work-main-solved.txt"
