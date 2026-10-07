import sys,os,importlib.util,shutil
from pathlib import Path
skill=Path(r'C:/Users/周雨杰/.codex/plugins/cache/openai-primary-runtime/documents/26.909.12148/skills/documents')
spec=importlib.util.spec_from_file_location('render_docx',skill/'render_docx.py');mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
# Native Word-compatible export replaces unavailable LibreOffice; keep packaged rasterization and page geometry handling.
pdf=Path(sys.argv[2]).resolve()
def exported(doc_path,user_profile,convert_tmp_dir,stem,verbose):
 dest=Path(convert_tmp_dir)/(stem+'.pdf');shutil.copyfile(pdf,dest);return str(dest),'Native Word compatible COM export'
mod.convert_to_pdf=exported
os.environ['PATH']=r'C:/Users/周雨杰/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/poppler/Library/bin'+os.pathsep+os.environ['PATH']
sys.argv=[str(skill/'render_docx.py'),sys.argv[1],'--output_dir',sys.argv[3],'--emit_pdf']
mod.main()
