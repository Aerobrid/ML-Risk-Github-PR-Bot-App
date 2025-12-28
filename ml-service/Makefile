.PHONY: help collect-data train retrain test-model clean

help:
	@echo "ML Service Training Commands"
	@echo "============================"
	@echo ""
	@echo "Usage:"
	@echo "  make collect-data REPO=owner/repo [LIMIT=1000]"
	@echo "  make train"
	@echo "  make retrain REPO=owner/repo"
	@echo "  make test-model"
	@echo "  make clean"
	@echo ""
	@echo "Examples:"
	@echo "  make collect-data REPO=microsoft/vscode LIMIT=500"
	@echo "  make train"
	@echo "  make retrain REPO=facebook/react"
	@echo ""
	@echo "Requirements:"
	@echo "  - GITHUB_TOKEN environment variable must be set"
	@echo "  - Python dependencies: pip install -r requirements.txt"

collect-data:
	@if [ -z "$(REPO)" ]; then \
		echo "Error: REPO parameter required"; \
		echo "Usage: make collect-data REPO=owner/repo"; \
		exit 1; \
	fi
	@if [ -z "$$GITHUB_TOKEN" ]; then \
		echo "Error: GITHUB_TOKEN environment variable not set"; \
		echo "Create token at: https://github.com/settings/tokens"; \
		exit 1; \
	fi
	@echo "Collecting historical data from $(REPO)..."
	python scripts/collect_training_data.py $(REPO) --limit $(or $(LIMIT),1000)

train:
	@echo "Training XGBoost model..."
	@python train_xgboost_model.py
	@echo ""
	@echo "Training complete! Model saved to:"
	@echo "  - models/xgboost_risk_model_v2.json"
	@echo "  - models/dummy_risk_model.pkl"

retrain: collect-data train
	@echo "Retraining complete!"
	@echo "Restart ML service to use new model:"
	@echo "  docker-compose restart ml-service"

test-model:
	@echo "Testing model with sample predictions..."
	@echo ""
	@echo "Test 1: Low risk (small change, business hours)"
	@curl -s -X POST http://localhost:8000/predict \
		-H "Content-Type: application/json" \
		-d '{"commitCount":2,"linesChanged":50,"testPassRate":1.0,"hourOfDay":14,"dayOfWeek":2,"files":[]}' \
		| python -m json.tool || echo "ML service not running on localhost:8000"
	@echo ""
	@echo "Test 2: High risk (large change, weekend, after hours)"
	@curl -s -X POST http://localhost:8000/predict \
		-H "Content-Type: application/json" \
		-d '{"commitCount":25,"linesChanged":1500,"testPassRate":0.8,"hourOfDay":22,"dayOfWeek":6,"files":[]}' \
		| python -m json.tool || echo "ML service not running on localhost:8000"

clean:
	@echo "Cleaning up generated files..."
	@rm -rf data/*.csv
	@rm -rf models/*.pkl models/*.json
	@rm -rf __pycache__ app/__pycache__ app/*/__pycache__
	@echo "Clean complete!"
