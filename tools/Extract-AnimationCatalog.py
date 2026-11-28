"""Extract names and state/clip associations, never animation data (pip install UnityPy)."""
import argparse
import json
from pathlib import Path
import UnityPy

parser = argparse.ArgumentParser()
parser.add_argument("bundle", type=Path)
parser.add_argument("--output", type=Path, default=Path("Assets/player-animation-catalog.json"))
args = parser.parse_args()
environment = UnityPy.load(str(args.bundle))
objects = {obj.path_id: obj for obj in environment.objects}
controller = next(obj.read_typetree() for obj in environment.objects
                  if obj.type.name == "AnimatorController" and obj.peek_name() == "Player_animator")
names = dict(controller["m_TOS"])
clips = [objects[ref["m_PathID"]].peek_name() for ref in controller["m_AnimationClips"]]
states = []
for layer in controller["m_Controller"]["m_LayerArray"]:
    layer = layer["data"]
    machine = controller["m_Controller"]["m_StateMachineArray"][layer["m_StateMachineIndex"]]["data"]
    for state in machine["m_StateConstantArray"]:
        state = state["data"]
        ids = {node["data"]["m_ClipID"]
               for tree in state["m_BlendTreeConstantArray"]
               for node in tree["data"]["m_NodeArray"]
               if not node["data"]["m_ChildIndices"]}
        states.append(dict(Layer=names[layer["m_Binding"]], Path=names[state["m_FullPathID"]],
                           Clips=sorted({clips[i] for i in ids if i < len(clips)})))
args.output.write_text(json.dumps(states, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(f"Extracted {len(states)} states, {len(set(clips))} clip names")
