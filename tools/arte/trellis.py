"""Modelo 3D de una raza desde su ilustración de frente con TRELLIS (demo gratuita de Hugging Face; 9 oct 2026).
Uso: out/arte-venv/bin/python tools/arte/trellis.py <raza>  (lee out/arte/para-3d/<raza>_frente.png)."""
import shutil, sys
from gradio_client import Client, handle_file
race = sys.argv[1]
c = Client('trellis-community/TRELLIS', verbose=False)
c.predict(api_name='/start_session')
front = handle_file(f'out/arte/para-3d/{race}_frente.png')
views = [front, handle_file(f'out/arte/para-3d/{race}_side.png'), handle_file(f'out/arte/para-3d/{race}_back.png')]
pre = []
img = c.predict(image=front, api_name='/preprocess_image')
res = c.predict(image=handle_file(img), multiimages=pre, seed=7, ss_guidance_strength=7.5, ss_sampling_steps=12,
                slat_guidance_strength=3.0, slat_sampling_steps=12, multiimage_algo='stochastic', mesh_simplify=0.95,
                texture_size=1024, api_name='/generate_and_extract_glb')
print(res)
glb = res[2] if isinstance(res[2], str) else res[1]
shutil.copy(glb, f'out/arte/para-3d/trellis/{race}.glb')
vid = res[0]['video'] if isinstance(res[0], dict) else res[0]
shutil.copy(vid, f'out/arte/para-3d/trellis/{race}.mp4')
print('ok')
